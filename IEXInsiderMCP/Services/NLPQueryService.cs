using IEXInsiderMCP.Models;
using System.Text.RegularExpressions;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Natural Language Processing service for converting text queries to data queries
/// </summary>
public class NLPQueryService
{
    private readonly IEXDataService _dataService;
    private readonly ILogger<NLPQueryService> _logger;

    public NLPQueryService(IEXDataService dataService, ILogger<NLPQueryService> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Process natural language query and return results
    /// </summary>
    public Task<QueryResult> ProcessQueryAsync(string query, int? limit = null)
    {
        try
        {
            _logger.LogInformation($"Processing query: {query}");

            var normalizedQuery = query.ToLowerInvariant();
            IEnumerable<IEXMarketData>? data = null;
            Dictionary<string, object>? aggregations = null;

            // Market type detection
            if (Regex.IsMatch(normalizedQuery, @"\b(dam|day\s*ahead|day-ahead)\b"))
            {
                data = _dataService.GetDataByType("DAM");
            }
            else if (Regex.IsMatch(normalizedQuery, @"\b(gdam|green\s*day\s*ahead|green day-ahead)\b"))
            {
                data = _dataService.GetDataByType("GDAM");
            }
            else if (Regex.IsMatch(normalizedQuery, @"\b(rtm|real\s*time|real-time)\b"))
            {
                data = _dataService.GetDataByType("RTM");
            }

            // Year detection
            var yearMatch = Regex.Match(normalizedQuery, @"\b(202[3-5])\b");
            if (yearMatch.Success)
            {
                int year = int.Parse(yearMatch.Groups[1].Value);
                var yearData = _dataService.GetDataByYear(year);
                data = data == null ? yearData : data.Intersect(yearData);
            }

            // Date detection
            var dateMatch = Regex.Match(normalizedQuery, @"(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})");
            if (dateMatch.Success)
            {
                try
                {
                    int day = int.Parse(dateMatch.Groups[1].Value);
                    int month = int.Parse(dateMatch.Groups[2].Value);
                    int year = int.Parse(dateMatch.Groups[3].Value);
                    if (year < 100) year += 2000;

                    var date = new DateTime(year, month, day);
                    var dateData = _dataService.GetDataByDate(date);
                    data = data == null ? dateData : data.Intersect(dateData);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error parsing date: {ex.Message}");
                }
            }

            // Month name detection
            var monthMatch = Regex.Match(normalizedQuery,
                @"\b(january|february|march|april|may|june|july|august|september|october|november|december)\b");
            if (monthMatch.Success)
            {
                var monthName = monthMatch.Groups[1].Value;
                int month = DateTime.ParseExact(monthName, "MMMM", System.Globalization.CultureInfo.InvariantCulture).Month;

                // Try to get year from context
                yearMatch = Regex.Match(normalizedQuery, @"\b(202[3-5])\b");
                int year = yearMatch.Success ? int.Parse(yearMatch.Groups[1].Value) : DateTime.Now.Year;

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);
                var monthData = _dataService.GetDataByDateRange(startDate, endDate);
                data = data == null ? monthData : data.Intersect(monthData);
            }

            // Aggregation queries
            if (Regex.IsMatch(normalizedQuery, @"\b(average|avg|mean)\b.*\b(price|mcp)\b"))
            {
                var dataList = (data ?? _dataService.GetAllData()).ToList();
                aggregations = new Dictionary<string, object>
                {
                    ["AverageMCP"] = dataList.Any() ? dataList.Average(d => d.MCP) : 0,
                    ["Unit"] = "Rs./kWh"
                };
            }

            if (Regex.IsMatch(normalizedQuery, @"\b(highest|maximum|max|peak)\b.*\b(price|mcp)\b"))
            {
                data = _dataService.GetPeakPriceData(limit ?? 10);
            }

            if (Regex.IsMatch(normalizedQuery, @"\b(lowest|minimum|min)\b.*\b(price|mcp)\b"))
            {
                var allData = (data ?? _dataService.GetAllData()).OrderBy(d => d.MCP);
                data = allData.Take(limit ?? 10);
            }

            if (Regex.IsMatch(normalizedQuery, @"\b(statistics|stats|summary)\b"))
            {
                aggregations = _dataService.GetStatistics();
            }

            if (Regex.IsMatch(normalizedQuery, @"\b(total|count|number of)\b.*\b(records?|transactions?|entries)\b"))
            {
                var dataList = (data ?? _dataService.GetAllData()).ToList();
                aggregations = new Dictionary<string, object>
                {
                    ["TotalRecords"] = dataList.Count
                };
            }

            // If no specific query matched, return all data
            if (data == null && aggregations == null)
            {
                data = _dataService.GetAllData();
            }

            // Apply limit
            if (data != null && limit.HasValue)
            {
                data = data.Take(limit.Value);
            }

            var resultData = data?.ToList();

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = "Query processed successfully",
                Data = resultData,
                Aggregations = aggregations,
                TotalRecords = resultData?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query");
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Message = $"Error processing query: {ex.Message}",
                TotalRecords = 0
            });
        }
    }

    /// <summary>
    /// Get query suggestions based on input
    /// </summary>
    public List<string> GetQuerySuggestions(string partialQuery)
    {
        var suggestions = new List<string>
        {
            "Show me DAM prices for 2024",
            "What is the average price in RTM market?",
            "Show peak prices in September 2024",
            "Get statistics for GDAM market",
            "Show me data for 01/01/2024",
            "What are the highest prices in 2025?",
            "Show me lowest prices in January 2024",
            "Get total records for RTM",
            "Show me average MCP by market type",
            "What was the market clearing volume yesterday?"
        };

        if (string.IsNullOrWhiteSpace(partialQuery))
        {
            return suggestions;
        }

        var normalized = partialQuery.ToLowerInvariant();
        return suggestions.Where(s => s.ToLowerInvariant().Contains(normalized)).ToList();
    }
}
