using Microsoft.AspNetCore.Mvc;
using IEXInsiderMCP.Models;
using IEXInsiderMCP.Services;

namespace IEXInsiderMCP.Controllers;

/// <summary>
/// Unified Query Controller - Single intelligent endpoint for all user queries
/// Routes to appropriate controller (IEX for data, Insights for AI) based on query analysis
/// This eliminates the need for frontend to decide which endpoint to call
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly NaturalLanguageEngine _nlEngine;
    private readonly MCPServer _mcpServer;
    private readonly InsightsEngine _insightsEngine;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        NaturalLanguageEngine nlEngine,
        MCPServer mcpServer,
        InsightsEngine insightsEngine,
        ILogger<QueryController> logger)
    {
        _nlEngine = nlEngine;
        _mcpServer = mcpServer;
        _insightsEngine = insightsEngine;
        _logger = logger;
    }

    /// <summary>
    /// Universal endpoint for ALL user queries
    /// Automatically determines if query needs AI insights or structured data
    /// Returns appropriate response type
    /// </summary>
    /// <remarks>
    /// Examples:
    ///
    /// AI Queries (returns IntelligentResponse with answer, charts, recommendations):
    /// - "What is the peak value for MCP in all markets in 2023?"
    /// - "Compare all three markets and tell me which is best"
    /// - "Give me insights about DAM market volatility"
    /// - "Should I buy or sell in GDAM?"
    /// - "Forecast the next 30 days"
    ///
    /// Data Queries (returns QueryResult with data/aggregations):
    /// - "Show me all DAM records from 2024"
    /// - "Get records where price is between 9 and 10"
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<object>> ProcessQuery([FromBody] UnifiedQueryRequest request)
    {
        try
        {
            _logger.LogInformation("Processing unified query: {Query}", request.Question ?? request.Query);

            var query = request.Question ?? request.Query ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { success = false, message = "Query cannot be empty" });
            }

            // Intelligently determine query type
            var queryType = DetermineQueryType(query, request);

            _logger.LogInformation("Query classified as: {QueryType}", queryType);

            if (queryType == QueryType.AIInsights)
            {
                // Route to AI Natural Language Engine
                var aiResponse = _nlEngine.ProcessQuery(query);
                return Ok(aiResponse);
            }
            else
            {
                // Route to structured data query via MCP Server
                var dataResponse = await ExecuteDataQuery(request);
                return Ok(dataResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing unified query");
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while processing your query",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Determine if query should be handled by AI engine or data service
    /// </summary>
    private QueryType DetermineQueryType(string query, UnifiedQueryRequest request)
    {
        var queryLower = query.ToLower();

        // Explicit override: if specific filters/aggregations provided, treat as data query
        if (request.Filters != null && request.Filters.Count > 0 &&
            !ContainsAIKeywords(queryLower))
        {
            return QueryType.StructuredData;
        }

        // AI indicators - these trigger intelligent analysis
        var aiIndicators = new[]
        {
            // Question words
            "what", "which", "why", "how", "when", "where",

            // Analysis keywords
            "insight", "analyze", "analysis", "tell me", "explain",
            "understand", "show me", "give me",

            // Comparison keywords
            "compare", "comparison", "versus", "vs", "better", "best", "worst",
            "all markets", "each market", "every market",

            // Forecasting keywords
            "forecast", "predict", "prediction", "future", "expect", "next",

            // Recommendation keywords
            "recommend", "recommendation", "should i", "buy", "sell", "invest",
            "advice", "suggest",

            // Pattern/Anomaly keywords
            "pattern", "patterns", "anomaly", "anomalies", "unusual", "spike", "spikes", "strange", "abnormal",
            "outlier", "outliers", "trend", "trending",

            // Statistical keywords (when asking questions)
            "peak", "highest", "lowest", "maximum", "minimum", "average",
            "most", "least", "best", "worst",

            // Advanced analytics keywords
            "performance", "standard deviation", "std dev", "stddev", "variance", "variability",
            "year wise", "yearly", "year-wise", "annual", "deviation",
            "till", "through", "from 20", "to 20"  // Year range patterns
        };

        // Check if query contains AI indicators
        foreach (var indicator in aiIndicators)
        {
            if (queryLower.Contains(indicator))
            {
                _logger.LogInformation("AI indicator found: {Indicator}", indicator);
                return QueryType.AIInsights;
            }
        }

        // Default to structured data for simple queries
        return QueryType.StructuredData;
    }

    private bool ContainsAIKeywords(string queryLower)
    {
        var keywords = new[] { "insight", "analyze", "compare", "forecast", "recommend", "tell me", "explain" };
        return keywords.Any(k => queryLower.Contains(k));
    }

    /// <summary>
    /// Execute structured data query using MCP Server
    /// </summary>
    private async Task<object> ExecuteDataQuery(UnifiedQueryRequest request)
    {
        var query = request.Question ?? request.Query ?? string.Empty;

        // Build filters from request
        var filters = new Dictionary<string, object>();

        if (request.Filters != null)
        {
            foreach (var filter in request.Filters)
            {
                filters[filter.Key] = filter.Value;
            }
        }

        // Parse query to extract additional filters
        var extractedFilters = ExtractFiltersFromQuery(query);
        foreach (var filter in extractedFilters)
        {
            if (!filters.ContainsKey(filter.Key))
            {
                filters[filter.Key] = filter.Value;
            }
        }

        // Detect aggregation and grouping from query
        var aggregation = request.Aggregation ?? DetectAggregation(query);
        var groupBy = request.GroupBy ?? DetectGrouping(query);

        // Add to filters if detected
        if (!string.IsNullOrEmpty(aggregation))
        {
            filters["aggregation"] = aggregation;
        }

        if (!string.IsNullOrEmpty(groupBy))
        {
            filters["group_by"] = groupBy;
        }

        // Build arguments for MCP server
        var arguments = new Dictionary<string, object>
        {
            ["query"] = query
        };

        if (request.Limit.HasValue)
        {
            arguments["limit"] = request.Limit.Value;
        }

        if (filters.Count > 0)
        {
            arguments["filters"] = filters;
        }

        // Execute via MCP Server
        var response = await _mcpServer.ExecuteToolAsync("query_iex_data", arguments);

        return response.Result ?? new { success = false, message = response.Error };
    }

    private Dictionary<string, object> ExtractFiltersFromQuery(string query)
    {
        var queryLower = query.ToLower();

        // Extract comprehensive date/time information
        var dateInfo = DateTimeExtractor.ExtractDateTimeInfo(query);
        var filters = DateTimeExtractor.ToFiltersDictionary(dateInfo);

        // Extract market type
        if (queryLower.Contains("dam") && !queryLower.Contains("gdam"))
        {
            filters["market_type"] = "DAM";
        }
        else if (queryLower.Contains("gdam"))
        {
            filters["market_type"] = "GDAM";
        }
        else if (queryLower.Contains("rtm"))
        {
            filters["market_type"] = "RTM";
        }

        return filters;
    }

    private string? DetectAggregation(string query)
    {
        var queryLower = query.ToLower();

        if (queryLower.Contains("average") || queryLower.Contains("avg") || queryLower.Contains("mean"))
            return "average";
        if (queryLower.Contains("highest") || queryLower.Contains("maximum") || queryLower.Contains("max") || queryLower.Contains("peak"))
            return "max";
        if (queryLower.Contains("lowest") || queryLower.Contains("minimum") || queryLower.Contains("min"))
            return "min";
        if (queryLower.Contains("total") || queryLower.Contains("sum"))
            return "sum";
        if (queryLower.Contains("count") || queryLower.Contains("how many") || queryLower.Contains("number of"))
            return "count";

        return null;
    }

    private string? DetectGrouping(string query)
    {
        var queryLower = query.ToLower();

        if (queryLower.Contains("each month") || queryLower.Contains("by month") || queryLower.Contains("monthly") || queryLower.Contains("which month"))
            return "month";
        if (queryLower.Contains("each year") || queryLower.Contains("by year") || queryLower.Contains("yearly"))
            return "year";
        if (queryLower.Contains("each market") || queryLower.Contains("by market"))
            return "market_type";
        if (queryLower.Contains("each day") || queryLower.Contains("by day") || queryLower.Contains("daily"))
            return "date";
        if (queryLower.Contains("each hour") || queryLower.Contains("by hour") || queryLower.Contains("hourly"))
            return "hour";

        return null;
    }

    /// <summary>
    /// Get suggested sample queries to help users discover capabilities
    /// </summary>
    [HttpGet("suggestions")]
    public ActionResult<SuggestedQueriesResponse> GetSuggestedQueries()
    {
        var suggestions = new SuggestedQueriesResponse
        {
            Categories = new List<QueryCategory>
            {
                new QueryCategory
                {
                    Name = "Market Comparison",
                    Icon = "📊",
                    Description = "Compare performance across different markets",
                    Queries = new List<string>
                    {
                        "Compare all markets for the last 30 days",
                        "What is the peak value for MCP in all markets in 2023?",
                        "Which market has the lowest average price?",
                        "Compare DAM and GDAM markets",
                        "Show me the most stable market"
                    }
                },
                new QueryCategory
                {
                    Name = "Advanced Analytics",
                    Icon = "📈",
                    Description = "Multi-year analysis and statistical insights",
                    Queries = new List<string>
                    {
                        "Chart for DAM, GDAM, RTM performance from 2023 till 2025",
                        "Standard deviation chart for DAM MCP in November 2023 and November 2024",
                        "Year wise comparison for all DAM, GDAM and RTM with MCP and MCV",
                        "Show me MCP trends for the last 3 years"
                    }
                },
                new QueryCategory
                {
                    Name = "Price & Volume Analysis",
                    Icon = "💰",
                    Description = "Analyze tariff ranges and volume patterns",
                    Queries = new List<string>
                    {
                        "In 2023 how many time blocks are within 9-10Rs for MCP",
                        "Show tariff range distribution for all markets",
                        "What percentage of slots fall in the 5-7 Rs range?",
                        "Analyze volume ranges for GDAM market"
                    }
                },
                new QueryCategory
                {
                    Name = "Time Slot Analysis",
                    Icon = "⏰",
                    Description = "Peak hours and time-based patterns",
                    Queries = new List<string>
                    {
                        "Which time slots have higher MCP in DAM?",
                        "Show peak hours for all markets",
                        "What are the off-peak hours in RTM?",
                        "Compare time slot prices across markets"
                    }
                },
                new QueryCategory
                {
                    Name = "Recommendations",
                    Icon = "💡",
                    Description = "Get buying and selling recommendations",
                    Queries = new List<string>
                    {
                        "Should I buy or sell in DAM market?",
                        "Which market is best for buying right now?",
                        "Give me trading recommendations for tomorrow",
                        "What's the best market for selling energy?"
                    }
                },
                new QueryCategory
                {
                    Name = "Trends & Patterns",
                    Icon = "📉",
                    Description = "Identify trends and anomalies",
                    Queries = new List<string>
                    {
                        "Show me price trends for DAM in 2024",
                        "Are there any anomalies in RTM prices?",
                        "What patterns do you see in GDAM volume?",
                        "Show unusual price spikes in the last month"
                    }
                }
            }
        };

        return Ok(suggestions);
    }
}

/// <summary>
/// Query type classification
/// </summary>
public enum QueryType
{
    AIInsights,      // Handled by NaturalLanguageEngine - returns IntelligentResponse
    StructuredData   // Handled by MCPServer - returns QueryResult
}

/// <summary>
/// Unified query request model - accepts both natural language and structured parameters
/// </summary>
public class UnifiedQueryRequest
{
    /// <summary>Natural language question (preferred for AI queries)</summary>
    public string? Question { get; set; }

    /// <summary>Query string (alternative to Question)</summary>
    public string? Query { get; set; }

    /// <summary>Optional filters for structured queries</summary>
    public Dictionary<string, object>? Filters { get; set; }

    /// <summary>Optional aggregation type: average, max, min, sum, count</summary>
    public string? Aggregation { get; set; }

    /// <summary>Optional grouping: month, year, market_type, date, hour</summary>
    public string? GroupBy { get; set; }

    /// <summary>Result limit for data queries</summary>
    public int? Limit { get; set; }
}

/// <summary>
/// Response model for suggested queries
/// </summary>
public class SuggestedQueriesResponse
{
    public List<QueryCategory> Categories { get; set; } = new();
}

/// <summary>
/// Category of related queries
/// </summary>
public class QueryCategory
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Queries { get; set; } = new();
}
