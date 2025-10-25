using IEXInsiderMCP.Models;
using System.Collections.Concurrent;
using System.Globalization;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Service for loading and querying IEX market data from CSV
/// </summary>
public class IEXDataService
{
    private readonly string _csvFilePath;
    private readonly ConcurrentBag<IEXMarketData> _marketData;
    private bool _isLoaded = false;
    private readonly ILogger<IEXDataService> _logger;

    public IEXDataService(IConfiguration configuration, ILogger<IEXDataService> logger)
    {
        _csvFilePath = configuration["IEXData:CsvFilePath"] ??
            @"C:\POCs\IEXInsider\IEX_Market_Data.csv";
        _marketData = new ConcurrentBag<IEXMarketData>();
        _logger = logger;
    }

    /// <summary>
    /// Load data from CSV file
    /// </summary>
    public async Task LoadDataAsync()
    {
        if (_isLoaded)
        {
            _logger.LogInformation("Data already loaded");
            return;
        }

        _logger.LogInformation($"Loading data from {_csvFilePath}");

        await Task.Run(() =>
        {
            using var reader = new StreamReader(_csvFilePath);

            // Skip header
            var header = reader.ReadLine();
            _logger.LogInformation($"CSV Header: {header}");

            int rowNumber = 2;
            int successCount = 0;
            int errorCount = 0;

            while (!reader.EndOfStream)
            {
                try
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = ParseCsvLine(line);

                    if (values.Length < 8 || string.IsNullOrWhiteSpace(values[0]))
                    {
                        continue;
                    }

                    var type = values[0].Trim();

                    if (!int.TryParse(values[1], out var year))
                    {
                        errorCount++;
                        continue;
                    }

                    if (!DateTime.TryParse(values[2], out var date))
                    {
                        errorCount++;
                        continue;
                    }

                    var timeBlock = values[3].Trim();

                    decimal.TryParse(values[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var demand);
                    decimal.TryParse(values[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var supply);

                    if (!decimal.TryParse(values[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var mcpValue))
                    {
                        errorCount++;
                        continue;
                    }

                    if (!decimal.TryParse(values[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var mcvValue))
                    {
                        errorCount++;
                        continue;
                    }

                    var marketData = new IEXMarketData
                    {
                        Type = type,
                        Year = year,
                        Date = date,
                        TimeBlock = timeBlock,
                        IEXDemand = demand,
                        IEXSupply = supply,
                        MCP = mcpValue,
                        MCV = mcvValue
                    };

                    _marketData.Add(marketData);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error parsing row {rowNumber}: {ex.Message}");
                    errorCount++;
                }

                rowNumber++;
            }

            _logger.LogInformation($"Successfully loaded {successCount} records, {errorCount} errors");
        });

        _isLoaded = true;
        _logger.LogInformation($"Total records in memory: {_marketData.Count}");
    }

    /// <summary>
    /// Parse CSV line handling quoted fields
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var currentField = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        result.Add(currentField.ToString());
        return result.ToArray();
    }

    /// <summary>
    /// Get all market data
    /// </summary>
    public IEnumerable<IEXMarketData> GetAllData()
    {
        _logger.LogDebug("GetAllData called");
        EnsureDataLoaded();
        _logger.LogDebug("GetAllData returning {Count} records", _marketData.Count);
        return _marketData;
    }

    /// <summary>
    /// Filter data by market type
    /// </summary>
    public IEnumerable<IEXMarketData> GetDataByType(string type)
    {
        _logger.LogInformation("GetDataByType called - Type: {Type}", type);
        EnsureDataLoaded();
        var result = _marketData.Where(d => d.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
        var count = result.Count();
        _logger.LogInformation("GetDataByType returning {Count} records for type {Type}", count, type);
        return result;
    }

    /// <summary>
    /// Filter data by date range
    /// </summary>
    public IEnumerable<IEXMarketData> GetDataByDateRange(DateTime startDate, DateTime endDate)
    {
        EnsureDataLoaded();
        return _marketData.Where(d => d.Date >= startDate && d.Date <= endDate);
    }

    /// <summary>
    /// Filter data by year
    /// </summary>
    public IEnumerable<IEXMarketData> GetDataByYear(int year)
    {
        EnsureDataLoaded();
        return _marketData.Where(d => d.Year == year);
    }

    /// <summary>
    /// Get average MCP by market type
    /// </summary>
    public Dictionary<string, decimal> GetAverageMCPByType()
    {
        EnsureDataLoaded();
        return _marketData
            .GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.Average(d => d.MCP));
    }

    /// <summary>
    /// Get peak price data
    /// </summary>
    public IEnumerable<IEXMarketData> GetPeakPriceData(int topN = 10)
    {
        EnsureDataLoaded();
        return _marketData.OrderByDescending(d => d.MCP).Take(topN);
    }

    /// <summary>
    /// Get data for a specific date
    /// </summary>
    public IEnumerable<IEXMarketData> GetDataByDate(DateTime date)
    {
        EnsureDataLoaded();
        return _marketData.Where(d => d.Date.Date == date.Date);
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public Dictionary<string, object> GetStatistics()
    {
        _logger.LogInformation("GetStatistics called");
        EnsureDataLoaded();

        var stats = new Dictionary<string, object>
        {
            ["TotalRecords"] = _marketData.Count,
            ["AvgMCP"] = _marketData.Average(d => d.MCP),
            ["MaxMCP"] = _marketData.Max(d => d.MCP),
            ["MinMCP"] = _marketData.Min(d => d.MCP),
            ["AvgMCV"] = _marketData.Average(d => d.MCV),
            ["MaxMCV"] = _marketData.Max(d => d.MCV),
            ["MinMCV"] = _marketData.Min(d => d.MCV),
            ["MarketTypes"] = _marketData.Select(d => d.Type).Distinct().ToList(),
            ["DateRange"] = new
            {
                Start = _marketData.Min(d => d.Date),
                End = _marketData.Max(d => d.Date)
            }
        };

        _logger.LogInformation("GetStatistics completed - TotalRecords: {Total}, AvgMCP: {AvgMCP:F4}",
            stats["TotalRecords"], stats["AvgMCP"]);

        return stats;
    }

    private void EnsureDataLoaded()
    {
        if (!_isLoaded)
        {
            throw new InvalidOperationException("Data not loaded. Call LoadDataAsync first.");
        }
    }

    /// <summary>
    /// Get total record count
    /// </summary>
    public int GetRecordCount()
    {
        return _marketData.Count;
    }
}
