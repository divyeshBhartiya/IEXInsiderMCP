using IEXInsiderMCP.Models;
using System.Text.RegularExpressions;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Advanced analytics service for complex queries:
/// - Multi-year range analysis (e.g., 2023-2025)
/// - Standard deviation calculations
/// - Year-wise comparisons
/// - Specific date ranges
/// </summary>
public class AdvancedAnalyticsService
{
    private readonly IEXDataService _dataService;
    private readonly ILogger<AdvancedAnalyticsService> _logger;

    public AdvancedAnalyticsService(IEXDataService dataService, ILogger<AdvancedAnalyticsService> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze performance across multiple years
    /// Example: "Chart for DAM, GDAM, RTM performance from 2023 till 2025"
    /// </summary>
    public async Task<MultiYearPerformanceResponse> AnalyzeMultiYearPerformance(string query)
    {
        await Task.CompletedTask;

        var response = new MultiYearPerformanceResponse
        {
            Success = true
        };

        // Parse years from query
        var yearMatch = Regex.Match(query, @"(\d{4})\s*(till|to|through|-)\s*(\d{4})");
        if (yearMatch.Success)
        {
            response.StartYear = int.Parse(yearMatch.Groups[1].Value);
            response.EndYear = int.Parse(yearMatch.Groups[3].Value);
        }
        else
        {
            // Try single year with "from YYYY"
            var singleYearMatch = Regex.Match(query, @"from\s+(\d{4})");
            if (singleYearMatch.Success)
            {
                response.StartYear = int.Parse(singleYearMatch.Groups[1].Value);
                response.EndYear = DateTime.Now.Year;
            }
        }

        // Parse markets - use word boundaries to avoid false matches
        var markets = new List<MarketType>();
        var queryLower = query.ToLower();

        // Check for GDAM/G-DAM
        if (Regex.IsMatch(queryLower, @"\b(gdam|g-dam)\b"))
            markets.Add(MarketType.GDAM);

        // Check for standalone DAM (not preceded by 'g' or '-')
        // Matches: "DAM", " DAM", ",DAM", but not "GDAM" or "G-DAM"
        if (Regex.IsMatch(queryLower, @"(?<!g)(?<!-)dam\b"))
            markets.Add(MarketType.DAM);

        // Check for RTM
        if (Regex.IsMatch(queryLower, @"\brtm\b"))
            markets.Add(MarketType.RTM);

        // If no markets specified, default to all
        if (!markets.Any())
            markets = new List<MarketType> { MarketType.DAM, MarketType.GDAM, MarketType.RTM };

        response.Markets = markets;

        // Parse metrics
        var hasMCP = query.Contains("MCP", StringComparison.OrdinalIgnoreCase) || query.Contains("price", StringComparison.OrdinalIgnoreCase);
        var hasMCV = query.Contains("MCV", StringComparison.OrdinalIgnoreCase) || query.Contains("volume", StringComparison.OrdinalIgnoreCase);

        // For multi-year performance, default to showing both metrics for comprehensive view
        if (!hasMCP && !hasMCV)
        {
            hasMCP = true;
            hasMCV = true;  // Show both price and volume trends
        }

        response.Metrics = new List<string>();
        if (hasMCP) response.Metrics.Add("MCP");
        if (hasMCV) response.Metrics.Add("MCV");

        // Get data for each year and market
        var yearlyData = new Dictionary<int, Dictionary<string, (decimal avgMCP, decimal avgMCV, int count)>>();

        for (int year = response.StartYear; year <= response.EndYear; year++)
        {
            yearlyData[year] = new Dictionary<string, (decimal, decimal, int)>();

            foreach (var market in markets)
            {
                var data = _dataService.GetAllData()
                    .Where(d => d.Year == year && d.Type.Equals(market.ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (data.Any())
                {
                    var avgMCP = data.Average(d => d.MCP);
                    var avgMCV = data.Average(d => d.MCV);
                    yearlyData[year][market.ToString()] = ((decimal)avgMCP, (decimal)avgMCV, data.Count);
                }
            }
        }

        response.YearlyData = yearlyData;
        response.Message = $"Multi-year performance analysis from {response.StartYear} to {response.EndYear}";

        return response;
    }

    /// <summary>
    /// Calculate standard deviation for a specific metric
    /// Example: "Standard deviation chart for DAM MCP in November 2023 and November 2024"
    /// </summary>
    public async Task<StandardDeviationResponse> AnalyzeStandardDeviation(string query)
    {
        await Task.CompletedTask;

        var response = new StandardDeviationResponse
        {
            Success = true
        };

        // Parse market
        MarketType market = MarketType.DAM;
        if (query.Contains("GDAM", StringComparison.OrdinalIgnoreCase) || query.Contains("G-DAM", StringComparison.OrdinalIgnoreCase))
            market = MarketType.GDAM;
        else if (query.Contains("RTM", StringComparison.OrdinalIgnoreCase))
            market = MarketType.RTM;

        response.Market = market;

        // Parse metric
        var metric = "MCP";
        if (query.Contains("MCV", StringComparison.OrdinalIgnoreCase) || query.Contains("volume", StringComparison.OrdinalIgnoreCase))
            metric = "MCV";

        response.Metric = metric;

        // Parse time periods (e.g., "November 2023 and November 2024")
        var monthYearMatches = Regex.Matches(query, @"(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{4})", RegexOptions.IgnoreCase);

        var periods = new List<(string month, int year)>();
        foreach (Match match in monthYearMatches)
        {
            var month = match.Groups[1].Value;
            var year = int.Parse(match.Groups[2].Value);
            periods.Add((month, year));
        }

        if (!periods.Any())
        {
            // Default to current month and year
            periods.Add((DateTime.Now.ToString("MMMM"), DateTime.Now.Year));
        }

        response.Periods = new List<PeriodStdDevData>();

        foreach (var (month, year) in periods)
        {
            var monthNumber = GetMonthNumber(month);
            if (!monthNumber.HasValue) continue;

            var data = _dataService.GetAllData()
                .Where(d => d.Year == year &&
                           d.Date.Month == monthNumber.Value &&
                           d.Type.Equals(market.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (data.Any())
            {
                var values = metric == "MCP" ? data.Select(d => (double)d.MCP).ToList() : data.Select(d => (double)d.MCV).ToList();
                var mean = values.Average();
                var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
                var stdDev = Math.Sqrt(variance);

                response.Periods.Add(new PeriodStdDevData
                {
                    PeriodName = $"{month} {year}",
                    Month = month,
                    Year = year,
                    Mean = (decimal)mean,
                    StandardDeviation = (decimal)stdDev,
                    Variance = (decimal)variance,
                    DataPoints = values.Count,
                    Min = (decimal)values.Min(),
                    Max = (decimal)values.Max()
                });
            }
        }

        response.Message = $"Standard deviation analysis for {market} {metric}";

        return response;
    }

    /// <summary>
    /// Year-wise comparison for all markets
    /// Example: "Year wise comparison for all DAM, GDAM and RTM with MCP and MCV"
    /// </summary>
    public async Task<YearWiseComparisonResponse> AnalyzeYearWiseComparison(string query)
    {
        await Task.CompletedTask;

        var response = new YearWiseComparisonResponse
        {
            Success = true
        };

        // Get all available years
        var allYears = _dataService.GetAllData()
            .Select(d => d.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        response.Years = allYears;

        // Parse markets - use word boundaries to avoid false matches
        var markets = new List<MarketType>();
        var queryLower = query.ToLower();

        // Check for GDAM/G-DAM
        if (Regex.IsMatch(queryLower, @"\b(gdam|g-dam)\b"))
            markets.Add(MarketType.GDAM);

        // Check for standalone DAM (not preceded by 'g' or '-')
        // Matches: "DAM", " DAM", ",DAM", but not "GDAM" or "G-DAM"
        if (Regex.IsMatch(queryLower, @"(?<!g)(?<!-)dam\b"))
            markets.Add(MarketType.DAM);

        // Check for RTM
        if (Regex.IsMatch(queryLower, @"\brtm\b"))
            markets.Add(MarketType.RTM);

        // If no markets specified, default to all
        if (!markets.Any())
            markets = new List<MarketType> { MarketType.DAM, MarketType.GDAM, MarketType.RTM };

        response.Markets = markets;

        // Parse metrics
        var hasMCP = query.Contains("MCP", StringComparison.OrdinalIgnoreCase) || query.Contains("price", StringComparison.OrdinalIgnoreCase);
        var hasMCV = query.Contains("MCV", StringComparison.OrdinalIgnoreCase) || query.Contains("volume", StringComparison.OrdinalIgnoreCase);

        if (!hasMCP && !hasMCV)
        {
            hasMCP = true;
            hasMCV = true;
        }

        response.IncludeMCP = hasMCP;
        response.IncludeMCV = hasMCV;

        // Collect data for each year and market
        response.ComparisonData = new Dictionary<int, Dictionary<string, YearlyMarketData>>();

        foreach (var year in allYears)
        {
            response.ComparisonData[year] = new Dictionary<string, YearlyMarketData>();

            foreach (var market in markets)
            {
                var data = _dataService.GetAllData()
                    .Where(d => d.Year == year && d.Type.Equals(market.ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (data.Any())
                {
                    response.ComparisonData[year][market.ToString()] = new YearlyMarketData
                    {
                        AvgMCP = (decimal)data.Average(d => d.MCP),
                        MaxMCP = (decimal)data.Max(d => d.MCP),
                        MinMCP = (decimal)data.Min(d => d.MCP),
                        AvgMCV = (decimal)data.Average(d => d.MCV),
                        MaxMCV = (decimal)data.Max(d => d.MCV),
                        MinMCV = (decimal)data.Min(d => d.MCV),
                        RecordCount = data.Count
                    };
                }
            }
        }

        response.Message = $"Year-wise comparison for {markets.Count} markets across {allYears.Count} years";

        return response;
    }

    private int? GetMonthNumber(string monthName)
    {
        return monthName.ToLower() switch
        {
            "january" or "jan" => 1,
            "february" or "feb" => 2,
            "march" or "mar" => 3,
            "april" or "apr" => 4,
            "may" => 5,
            "june" or "jun" => 6,
            "july" or "jul" => 7,
            "august" or "aug" => 8,
            "september" or "sep" or "sept" => 9,
            "october" or "oct" => 10,
            "november" or "nov" => 11,
            "december" or "dec" => 12,
            _ => null
        };
    }
}

// Response models
public class MultiYearPerformanceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public List<MarketType> Markets { get; set; } = new();
    public List<string> Metrics { get; set; } = new();
    public Dictionary<int, Dictionary<string, (decimal avgMCP, decimal avgMCV, int count)>> YearlyData { get; set; } = new();
}

public class StandardDeviationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MarketType Market { get; set; }
    public string Metric { get; set; } = string.Empty;
    public List<PeriodStdDevData> Periods { get; set; } = new();
}

public class PeriodStdDevData
{
    public string PeriodName { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal Mean { get; set; }
    public decimal StandardDeviation { get; set; }
    public decimal Variance { get; set; }
    public int DataPoints { get; set; }
    public decimal Min { get; set; }
    public decimal Max { get; set; }
}

public class YearWiseComparisonResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<int> Years { get; set; } = new();
    public List<MarketType> Markets { get; set; } = new();
    public bool IncludeMCP { get; set; }
    public bool IncludeMCV { get; set; }
    public Dictionary<int, Dictionary<string, YearlyMarketData>> ComparisonData { get; set; } = new();
}

public class YearlyMarketData
{
    public decimal AvgMCP { get; set; }
    public decimal MaxMCP { get; set; }
    public decimal MinMCP { get; set; }
    public decimal AvgMCV { get; set; }
    public decimal MaxMCV { get; set; }
    public decimal MinMCV { get; set; }
    public int RecordCount { get; set; }
}
