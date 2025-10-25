using IEXInsiderMCP.Models;
using Newtonsoft.Json;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Model Context Protocol service implementation
/// </summary>
public class MCPService
{
    private readonly IEXDataService _dataService;
    private readonly NLPQueryService _nlpService;
    private readonly ILogger<MCPService> _logger;

    public MCPService(
        IEXDataService dataService,
        NLPQueryService nlpService,
        ILogger<MCPService> logger)
    {
        _dataService = dataService;
        _nlpService = nlpService;
        _logger = logger;
    }

    /// <summary>
    /// Get available MCP tools
    /// </summary>
    public List<MCPTool> GetAvailableTools()
    {
        _logger.LogInformation("GetAvailableTools called - returning {ToolCount} MCP tools", 5);
        return new List<MCPTool>
        {
            new MCPTool
            {
                Name = "query_iex_data",
                Description = "Query Indian Energy Exchange market data using natural language. Supports queries about DAM, GDAM, RTM markets, prices (MCP), volumes (MCV), dates, and statistics.",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["query"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Natural language query about IEX market data"
                        },
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of records to return (optional)"
                        }
                    },
                    ["required"] = new[] { "query" }
                }
            },
            new MCPTool
            {
                Name = "get_market_statistics",
                Description = "Get comprehensive statistics about IEX market data including averages, peaks, and market type distribution.",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                }
            },
            new MCPTool
            {
                Name = "get_data_by_type",
                Description = "Get market data filtered by market type (DAM, GDAM, or RTM).",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["market_type"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["enum"] = new[] { "DAM", "GDAM", "RTM" },
                            ["description"] = "Market type to filter by"
                        },
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of records to return (optional)"
                        }
                    },
                    ["required"] = new[] { "market_type" }
                }
            },
            new MCPTool
            {
                Name = "get_peak_prices",
                Description = "Get the highest market clearing prices (MCP) from the dataset.",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["top_n"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "Number of top prices to return",
                            ["default"] = 10
                        }
                    }
                }
            },
            new MCPTool
            {
                Name = "get_data_by_date_range",
                Description = "Get market data for a specific date range.",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["start_date"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["format"] = "date",
                            ["description"] = "Start date (YYYY-MM-DD)"
                        },
                        ["end_date"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["format"] = "date",
                            ["description"] = "End date (YYYY-MM-DD)"
                        }
                    },
                    ["required"] = new[] { "start_date", "end_date" }
                }
            }
        };
    }

    /// <summary>
    /// Execute an MCP tool call
    /// </summary>
    public async Task<MCPToolCallResponse> ExecuteToolAsync(MCPToolCallRequest request)
    {
        try
        {
            _logger.LogInformation("ExecuteToolAsync called - ToolName: {ToolName}, Arguments: {Arguments}",
                request.ToolName,
                request.Arguments != null ? string.Join(", ", request.Arguments.Keys) : "none");

            return request.ToolName switch
            {
                "query_iex_data" => await ExecuteQueryIEXData(request.Arguments),
                "get_market_statistics" => await ExecuteGetStatistics(),
                "get_data_by_type" => await ExecuteGetDataByType(request.Arguments),
                "get_peak_prices" => await ExecuteGetPeakPrices(request.Arguments),
                "get_data_by_date_range" => await ExecuteGetDataByDateRange(request.Arguments),
                _ => new MCPToolCallResponse
                {
                    Success = false,
                    Error = $"Unknown tool: {request.ToolName}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing tool {request.ToolName}");
            return new MCPToolCallResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<MCPToolCallResponse> ExecuteQueryIEXData(Dictionary<string, object>? arguments)
    {
        _logger.LogInformation("ExecuteQueryIEXData started - Arguments: {ArgCount}", arguments?.Count ?? 0);

        if (arguments == null || !arguments.ContainsKey("query"))
        {
            _logger.LogWarning("ExecuteQueryIEXData failed - Missing required argument: query");
            return new MCPToolCallResponse
            {
                Success = false,
                Error = "Missing required argument: query"
            };
        }

        var query = arguments["query"].ToString();
        int? limit = arguments.ContainsKey("limit") ? Convert.ToInt32(arguments["limit"]) : null;

        _logger.LogInformation("ExecuteQueryIEXData - Query: '{Query}', Limit: {Limit}", query, limit);

        var result = await _nlpService.ProcessQueryAsync(query!, limit);

        _logger.LogInformation("ExecuteQueryIEXData completed - Success: {Success}, Records: {Records}",
            result.Success, result.TotalRecords);

        return new MCPToolCallResponse
        {
            Success = result.Success,
            Result = result
        };
    }

    private async Task<MCPToolCallResponse> ExecuteGetStatistics()
    {
        await Task.CompletedTask;
        var stats = _dataService.GetStatistics();

        return new MCPToolCallResponse
        {
            Success = true,
            Result = stats
        };
    }

    private async Task<MCPToolCallResponse> ExecuteGetDataByType(Dictionary<string, object>? arguments)
    {
        await Task.CompletedTask;

        if (arguments == null || !arguments.ContainsKey("market_type"))
        {
            return new MCPToolCallResponse
            {
                Success = false,
                Error = "Missing required argument: market_type"
            };
        }

        var marketType = arguments["market_type"].ToString();
        int? limit = arguments.ContainsKey("limit") ? Convert.ToInt32(arguments["limit"]) : null;

        var data = _dataService.GetDataByType(marketType!);

        if (limit.HasValue)
        {
            data = data.Take(limit.Value);
        }

        return new MCPToolCallResponse
        {
            Success = true,
            Result = new QueryResult
            {
                Success = true,
                Message = $"Data retrieved for market type: {marketType}",
                Data = data.ToList(),
                TotalRecords = data.Count()
            }
        };
    }

    private async Task<MCPToolCallResponse> ExecuteGetPeakPrices(Dictionary<string, object>? arguments)
    {
        await Task.CompletedTask;

        int topN = arguments != null && arguments.ContainsKey("top_n")
            ? Convert.ToInt32(arguments["top_n"])
            : 10;

        var data = _dataService.GetPeakPriceData(topN);

        return new MCPToolCallResponse
        {
            Success = true,
            Result = new QueryResult
            {
                Success = true,
                Message = $"Top {topN} peak prices retrieved",
                Data = data.ToList(),
                TotalRecords = data.Count()
            }
        };
    }

    private async Task<MCPToolCallResponse> ExecuteGetDataByDateRange(Dictionary<string, object>? arguments)
    {
        await Task.CompletedTask;

        if (arguments == null || !arguments.ContainsKey("start_date") || !arguments.ContainsKey("end_date"))
        {
            return new MCPToolCallResponse
            {
                Success = false,
                Error = "Missing required arguments: start_date and end_date"
            };
        }

        var startDate = DateTime.Parse(arguments["start_date"].ToString()!);
        var endDate = DateTime.Parse(arguments["end_date"].ToString()!);

        var data = _dataService.GetDataByDateRange(startDate, endDate);

        return new MCPToolCallResponse
        {
            Success = true,
            Result = new QueryResult
            {
                Success = true,
                Message = $"Data retrieved from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                Data = data.ToList(),
                TotalRecords = data.Count()
            }
        };
    }
}
