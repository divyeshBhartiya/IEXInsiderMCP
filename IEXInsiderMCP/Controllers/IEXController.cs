using IEXInsiderMCP.Models;
using IEXInsiderMCP.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace IEXInsiderMCP.Controllers;

/// <summary>
/// Unified IEX Data Controller
/// Single entry point for all IEX market data operations
/// Supports both REST API and MCP JSON-RPC 2.0 protocol
/// </summary>
[ApiController]
[Route("api/iex")]
public class IEXController : ControllerBase
{
    private readonly MCPServer _mcpServer;
    private readonly MultiTimeSlotAnalyzer _multiTimeSlotAnalyzer;
    private readonly AdvancedAnalyticsService _advancedAnalytics;
    private readonly ILogger<IEXController> _logger;

    public IEXController(
        MCPServer mcpServer,
        MultiTimeSlotAnalyzer multiTimeSlotAnalyzer,
        AdvancedAnalyticsService advancedAnalytics,
        ILogger<IEXController> logger)
    {
        _mcpServer = mcpServer;
        _multiTimeSlotAnalyzer = multiTimeSlotAnalyzer;
        _advancedAnalytics = advancedAnalytics;
        _logger = logger;
    }

    #region REST API Endpoints

    /// <summary>
    /// Universal query endpoint - Handles all types of queries
    /// Supports natural language, structured filters, and aggregations
    /// </summary>
    /// <remarks>
    /// Examples:
    ///
    /// Natural Language:
    /// POST /api/iex/query
    /// {
    ///   "query": "Show me DAM prices for 2024",
    ///   "limit": 10
    /// }
    ///
    /// Structured with filters:
    /// POST /api/iex/query
    /// {
    ///   "query": "Get data within price range",
    ///   "filters": {
    ///     "market_type": "DAM",
    ///     "year": 2023,
    ///     "mcp_min": 9,
    ///     "mcp_max": 10
    ///   },
    ///   "limit": 100
    /// }
    /// </remarks>
    [HttpPost("query")]
    public async Task<ActionResult<QueryResult>> Query([FromBody] UniversalQueryRequest request)
    {
        _logger.LogInformation("POST /api/iex/query - Query: '{Query}', HasFilters: {HasFilters}",
            request?.Query ?? "null",
            request?.Filters != null);

        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new QueryResult
                {
                    Success = false,
                    Message = "Query parameter is required"
                });
            }

            // Build arguments for MCP server
            var arguments = new Dictionary<string, object>
            {
                ["query"] = request.Query
            };

            if (request.Limit.HasValue)
            {
                arguments["limit"] = request.Limit.Value;
            }

            // Merge filters with aggregation/groupBy parameters
            var filters = request.Filters ?? new Dictionary<string, object>();

            _logger.LogInformation("Request - Aggregation: '{Agg}', GroupBy: '{Group}'",
                request.Aggregation ?? "null", request.GroupBy ?? "null");

            if (!string.IsNullOrEmpty(request.Aggregation))
            {
                filters["aggregation"] = request.Aggregation;
                _logger.LogInformation("Added aggregation to filters: {Agg}", request.Aggregation);
            }

            if (!string.IsNullOrEmpty(request.GroupBy))
            {
                filters["group_by"] = request.GroupBy;
                _logger.LogInformation("Added group_by to filters: {Group}", request.GroupBy);
            }

            if (filters.Count > 0)
            {
                arguments["filters"] = filters;
                _logger.LogInformation("Filters count: {Count}, Keys: {Keys}",
                    filters.Count, string.Join(", ", filters.Keys));
            }

            var response = await _mcpServer.ExecuteToolAsync("query_iex_data", arguments);

            if (!response.Success)
            {
                return BadRequest(new QueryResult
                {
                    Success = false,
                    Message = response.Error ?? "Query execution failed"
                });
            }

            return Ok(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query");
            return StatusCode(500, new QueryResult
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get comprehensive market statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<Dictionary<string, object>>> GetStatistics([FromQuery] string? marketType = null)
    {
        _logger.LogInformation("GET /api/iex/statistics - MarketType: {MarketType}", marketType ?? "ALL");

        try
        {
            var arguments = marketType != null
                ? new Dictionary<string, object> { ["market_type"] = marketType }
                : null;

            var response = await _mcpServer.ExecuteToolAsync("get_statistics", arguments);

            if (!response.Success)
            {
                return BadRequest(new { error = response.Error });
            }

            return Ok(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get query suggestions based on partial input
    /// </summary>
    [HttpGet("suggestions")]
    public ActionResult<List<string>> GetSuggestions([FromQuery] string? q = null)
    {
        try
        {
            var suggestions = _mcpServer.GetSuggestions(q);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestions");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate heat map from natural language query (INTELLIGENT)
    /// Automatically extracts: date/month/year, market type, time slots, metric
    /// </summary>
    /// <remarks>
    /// Examples:
    /// POST /api/iex/heatmap
    /// { "query": "Generate Heat Map for MCP for Aug 2025 for DAM market" }
    /// { "query": "Show heatmap of MCV for RTM in September 2024 from 5pm to 9pm" }
    /// { "query": "Create heat map for GDAM prices in June 2024" }
    /// </remarks>
    [HttpPost("heatmap")]
    public ActionResult<Dictionary<string, object>> GenerateHeatMapFromQuery([FromBody] HeatMapRequest request)
    {
        _logger.LogInformation("POST /api/iex/heatmap - Query: '{Query}'", request?.Query ?? "null");

        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { success = false, message = "Query parameter is required" });
            }

            var query = request.Query;
            var queryLower = query.ToLower();

            // Build filters by intelligently parsing the query
            var filters = new Dictionary<string, object>();

            // Extract date/time information using DateTimeExtractor
            var dateInfo = DateTimeExtractor.ExtractDateTimeInfo(query);

            _logger.LogInformation("DateTimeExtractor results - Year: {Year}, Month: {Month}, Day: {Day}, TimeBlockStart: '{Start}', TimeBlockEnd: '{End}'",
                dateInfo.Year, dateInfo.Month, dateInfo.Day, dateInfo.TimeBlockStart ?? "null", dateInfo.TimeBlockEnd ?? "null");

            if (dateInfo.Year.HasValue)
                filters["year"] = dateInfo.Year.Value;

            if (dateInfo.Month.HasValue)
                filters["month"] = dateInfo.Month.Value;

            if (dateInfo.Day.HasValue)
                filters["day"] = dateInfo.Day.Value;

            if (dateInfo.StartDate.HasValue)
                filters["start_date"] = dateInfo.StartDate.Value.ToString("yyyy-MM-dd");

            if (dateInfo.EndDate.HasValue)
                filters["end_date"] = dateInfo.EndDate.Value.ToString("yyyy-MM-dd");

            if (!string.IsNullOrEmpty(dateInfo.TimeBlockStart))
                filters["time_block_start"] = dateInfo.TimeBlockStart;

            if (!string.IsNullOrEmpty(dateInfo.TimeBlockEnd))
                filters["time_block_end"] = dateInfo.TimeBlockEnd;

            // Extract market type
            if (queryLower.Contains("dam") && !queryLower.Contains("gdam"))
                filters["market_type"] = "DAM";
            else if (queryLower.Contains("gdam"))
                filters["market_type"] = "GDAM";
            else if (queryLower.Contains("rtm"))
                filters["market_type"] = "RTM";

            // Extract metric type (MCP or MCV)
            string metric = "mcp"; // default
            if (queryLower.Contains("mcv") || queryLower.Contains("volume"))
                metric = "mcv";
            else if (queryLower.Contains("mcp") || queryLower.Contains("price"))
                metric = "mcp";

            _logger.LogInformation("Extracted filters: {Filters}, Metric: {Metric}",
                string.Join(", ", filters.Select(kv => $"{kv.Key}={kv.Value}")), metric);

            var heatMapData = _mcpServer.GetHeatMapData(filters.Count > 0 ? filters : null, metric);

            // Add query context to response
            if (heatMapData.ContainsKey("success") && (bool)heatMapData["success"])
            {
                heatMapData["query"] = query;
                heatMapData["extracted_filters"] = filters;
                heatMapData["metric_type"] = metric.ToUpperInvariant();
            }

            return Ok(heatMapData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating heat map from query");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get heat map data for visualization (LEGACY - Simple Parameters)
    /// </summary>
    /// <remarks>
    /// Example:
    /// GET /api/iex/heatmap?year=2024&metric=mcp
    /// GET /api/iex/heatmap?market_type=DAM&metric=mcv
    /// </remarks>
    [HttpGet("heatmap")]
    public ActionResult<Dictionary<string, object>> GetHeatMap(
        [FromQuery] string? market_type = null,
        [FromQuery] int? year = null,
        [FromQuery] string metric = "mcp")
    {
        _logger.LogInformation("GET /api/iex/heatmap - MarketType: {MarketType}, Year: {Year}, Metric: {Metric}",
            market_type ?? "ALL", year?.ToString() ?? "ALL", metric);

        try
        {
            var filters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(market_type))
            {
                filters["market_type"] = market_type;
            }

            if (year.HasValue)
            {
                filters["year"] = year.Value;
            }

            var heatMapData = _mcpServer.GetHeatMapData(filters.Count > 0 ? filters : null, metric);

            return Ok(heatMapData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating heat map");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Multi-time-slot analysis endpoint
    /// Generates multiple heat maps/charts for different time slots across markets
    /// </summary>
    /// <remarks>
    /// Examples:
    /// POST /api/iex/multi-timeslot
    /// {
    ///   "query": "Generate heat map for MCP for all 3 markets considering time slots: 9AM to 5PM, 5PM to 9PM, 9PM to 6AM, 6AM to 9AM"
    /// }
    ///
    /// POST /api/iex/multi-timeslot
    /// {
    ///   "query": "Generate bar chart for MCV for all markets: 9AM to 5PM, 5PM to 9PM"
    /// }
    /// </remarks>
    [HttpPost("multi-timeslot")]
    public async Task<ActionResult<MultiTimeSlotResponse>> AnalyzeMultiTimeSlot([FromBody] MultiTimeSlotRequest request)
    {
        _logger.LogInformation("POST /api/iex/multi-timeslot - Query: '{Query}'", request?.Query ?? "null");

        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new MultiTimeSlotResponse
                {
                    Success = false,
                    Message = "Query parameter is required"
                });
            }

            // Parse query if time slots not explicitly provided
            if (!request.TimeSlots.Any())
            {
                request = _multiTimeSlotAnalyzer.ParseQuery(request.Query);
            }

            _logger.LogInformation("Analyzing {MarketCount} markets across {TimeSlotCount} time slots",
                request.Markets.Count, request.TimeSlots.Count);

            var response = await _multiTimeSlotAnalyzer.AnalyzeAsync(request);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in multi-time-slot analysis");
            return StatusCode(500, new MultiTimeSlotResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            });
        }
    }

    #endregion

    #region MCP JSON-RPC 2.0 Endpoint

    /// <summary>
    /// JSON-RPC 2.0 endpoint for MCP protocol compliance
    /// </summary>
    /// <remarks>
    /// Examples:
    ///
    /// List available tools:
    /// POST /api/iex/jsonrpc
    /// {
    ///   "jsonrpc": "2.0",
    ///   "id": "1",
    ///   "method": "tools/list",
    ///   "params": {}
    /// }
    ///
    /// Execute tool:
    /// POST /api/iex/jsonrpc
    /// {
    ///   "jsonrpc": "2.0",
    ///   "id": "2",
    ///   "method": "tools/call",
    ///   "params": {
    ///     "name": "query_iex_data",
    ///     "arguments": {
    ///       "query": "Show me DAM prices for 2024",
    ///       "limit": 10
    ///     }
    ///   }
    /// }
    /// </remarks>
    [HttpPost("jsonrpc")]
    public async Task<ActionResult<JsonRpcResponse>> JsonRpc([FromBody] JsonRpcRequest request)
    {
        _logger.LogInformation("POST /api/iex/jsonrpc - Method: {Method}, Id: {Id}",
            request?.Method ?? "null",
            request?.Id ?? "null");

        // Validate JSON-RPC request
        if (request == null)
        {
            return BadRequest(CreateErrorResponse(null, JsonRpcErrorCodes.InvalidRequest, "Invalid Request"));
        }

        if (request.Jsonrpc != "2.0")
        {
            return Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, "JSON-RPC version must be 2.0"));
        }

        try
        {
            return request.Method switch
            {
                "tools/list" => await HandleToolsList(request),
                "tools/call" => await HandleToolsCall(request),
                _ => Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Method not found: {request.Method}"))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON-RPC error processing method: {Method}", request.Method);
            return Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, "Internal error", ex.Message));
        }
    }

    private Task<ActionResult<JsonRpcResponse>> HandleToolsList(JsonRpcRequest request)
    {
        _logger.LogInformation("JSON-RPC - tools/list");

        try
        {
            var tools = _mcpServer.ListTools();
            return Task.FromResult<ActionResult<JsonRpcResponse>>(Ok(new JsonRpcResponse
            {
                Id = request.Id,
                Result = new { tools }
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tools/list");
            return Task.FromResult<ActionResult<JsonRpcResponse>>(Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, "Error listing tools", ex.Message)));
        }
    }

    private async Task<ActionResult<JsonRpcResponse>> HandleToolsCall(JsonRpcRequest request)
    {
        _logger.LogInformation("JSON-RPC - tools/call");

        try
        {
            if (request.Params == null)
            {
                return Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing params"));
            }

            var paramsObj = JObject.FromObject(request.Params);
            var toolName = paramsObj["name"]?.ToString();
            var arguments = paramsObj["arguments"]?.ToObject<Dictionary<string, object>>();

            if (string.IsNullOrWhiteSpace(toolName))
            {
                return Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing tool name"));
            }

            _logger.LogInformation("JSON-RPC - Executing tool: {ToolName}", toolName);

            var result = await _mcpServer.ExecuteToolAsync(toolName, arguments);

            if (!result.Success)
            {
                return Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, result.Error ?? "Tool execution failed"));
            }

            return Ok(new JsonRpcResponse
            {
                Id = request.Id,
                Result = result.Result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tools/call");
            return Ok(CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, "Error executing tool", ex.Message));
        }
    }

    private JsonRpcResponse CreateErrorResponse(string? id, int code, string message, string? data = null)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };
    }

    #endregion

    #region Legacy Compatibility Routes (Deprecated)

    /// <summary>
    /// Legacy natural language query endpoint
    /// DEPRECATED: Use POST /api/iex/query instead
    /// </summary>
    [HttpPost("nl")]
    [Obsolete("Use POST /api/iex/query instead")]
    public async Task<ActionResult<QueryResult>> ProcessNaturalLanguage([FromBody] NLQueryRequest request)
    {
        _logger.LogWarning("DEPRECATED endpoint called: POST /api/iex/nl - Use /api/iex/query instead");

        var universalRequest = new UniversalQueryRequest
        {
            Query = request.Query,
            Limit = request.Limit
        };

        return await Query(universalRequest);
    }

    #endregion
}
