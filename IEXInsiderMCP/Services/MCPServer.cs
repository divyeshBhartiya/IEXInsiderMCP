using IEXInsiderMCP.Models;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Unified MCP Server - Single entry point for all IEX data operations
/// Implements Model Context Protocol with JSON-RPC 2.0 compliance
/// </summary>
public class MCPServer
{
    private readonly IEXDataService _dataService;
    private readonly NLPQueryService _nlpService;
    private readonly NaturalLanguageEngine _naturalLanguageEngine;
    private readonly ILogger<MCPServer> _logger;

    public MCPServer(
        IEXDataService dataService,
        NLPQueryService nlpService,
        NaturalLanguageEngine naturalLanguageEngine,
        ILogger<MCPServer> logger)
    {
        _dataService = dataService;
        _nlpService = nlpService;
        _naturalLanguageEngine = naturalLanguageEngine;
        _logger = logger;
    }

    #region MCP Protocol - JSON-RPC 2.0

    /// <summary>
    /// Get available MCP tools
    /// </summary>
    public List<MCPTool> ListTools()
    {
        _logger.LogInformation("MCP Server - ListTools called");
        return new List<MCPTool>
        {
            new MCPTool
            {
                Name = "query_iex_data",
                Description = "Universal query tool for IEX market data. Supports natural language, filters, aggregations, time ranges, and statistics.",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["query"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Natural language query or structured filter"
                        },
                        ["filters"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["description"] = "Optional filters: market_type, year, date_range, time_blocks, price_range",
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["market_type"] = new { type = "string", @enum = new[] { "DAM", "GDAM", "RTM" } },
                                ["year"] = new { type = "integer", minimum = 2023, maximum = 2025 },
                                ["start_date"] = new { type = "string", format = "date" },
                                ["end_date"] = new { type = "string", format = "date" },
                                ["time_blocks"] = new { type = "array", items = new { type = "string" } },
                                ["mcp_min"] = new { type = "number", description = "Minimum MCP (Rs/kWh)" },
                                ["mcp_max"] = new { type = "number", description = "Maximum MCP (Rs/kWh)" },
                                ["mcv_min"] = new { type = "number", description = "Minimum MCV (GW)" },
                                ["mcv_max"] = new { type = "number", description = "Maximum MCV (GW)" }
                            }
                        },
                        ["aggregation"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["enum"] = new[] { "average", "sum", "count", "max", "min", "stddev", "group_by" },
                            ["description"] = "Aggregation type to apply"
                        },
                        ["group_by"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["enum"] = new[] { "market_type", "year", "month", "date", "time_block" },
                            ["description"] = "Group results by field"
                        },
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of records to return"
                        }
                    },
                    ["required"] = new[] { "query" }
                }
            },
            new MCPTool
            {
                Name = "get_statistics",
                Description = "Get comprehensive market statistics including averages, peaks, standard deviation, and distribution.",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["market_type"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["enum"] = new[] { "DAM", "GDAM", "RTM", "ALL" },
                            ["description"] = "Filter statistics by market type (default: ALL)"
                        },
                        ["include_charts"] = new Dictionary<string, object>
                        {
                            ["type"] = "boolean",
                            ["description"] = "Include chart data in response",
                            ["default"] = false
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Execute MCP tool by name
    /// </summary>
    public async Task<MCPToolCallResponse> ExecuteToolAsync(string toolName, Dictionary<string, object>? arguments)
    {
        _logger.LogInformation("MCP Server - ExecuteTool: {ToolName}", toolName);

        try
        {
            return toolName switch
            {
                "query_iex_data" => await ExecuteUniversalQuery(arguments),
                "get_statistics" => await ExecuteGetStatistics(arguments),
                _ => new MCPToolCallResponse
                {
                    Success = false,
                    Error = $"Unknown tool: {toolName}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Server - Error executing tool: {ToolName}", toolName);
            return new MCPToolCallResponse
            {
                Success = false,
                Error = $"Tool execution failed: {ex.Message}"
            };
        }
    }

    #endregion

    #region Tool Implementations

    /// <summary>
    /// Universal query handler - supports NL, filters, aggregations
    /// </summary>
    private async Task<MCPToolCallResponse> ExecuteUniversalQuery(Dictionary<string, object>? arguments)
    {
        if (arguments == null || !arguments.ContainsKey("query"))
        {
            return new MCPToolCallResponse
            {
                Success = false,
                Error = "Missing required argument: query"
            };
        }

        var query = arguments["query"].ToString()!;
        int? limit = arguments.ContainsKey("limit") ? Convert.ToInt32(arguments["limit"]) : null;

        _logger.LogInformation("MCP Server - UniversalQuery: '{Query}', Limit: {Limit}", query, limit);

        // Check if filters are provided for structured query
        if (arguments.ContainsKey("filters"))
        {
            var result = await ProcessStructuredQuery(query, arguments["filters"], limit);
            return new MCPToolCallResponse
            {
                Success = result.Success,
                Result = result
            };
        }

        // Try Natural Language Engine first for advanced analytics queries
        var intelligentResponse = _naturalLanguageEngine.ProcessQuery(query);

        // If Natural Language Engine handled it successfully, return that response
        if (intelligentResponse != null && intelligentResponse.Success)
        {
            return new MCPToolCallResponse
            {
                Success = true,
                Result = intelligentResponse  // Return IntelligentResponse directly
            };
        }

        // Fall back to NLP query service for other queries
        var nlpResult = await _nlpService.ProcessQueryAsync(query, limit);
        return new MCPToolCallResponse
        {
            Success = nlpResult.Success,
            Result = nlpResult
        };
    }

    /// <summary>
    /// Process structured query with filters, aggregations, and grouping
    /// Pattern: Filter → Aggregate on FULL dataset → Apply limit to display data only
    /// </summary>
    private async Task<QueryResult> ProcessStructuredQuery(string query, object filtersObj, int? limit)
    {
        await Task.CompletedTask;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var filters = filtersObj as Dictionary<string, object> ?? new Dictionary<string, object>();

        // Extract aggregation and grouping parameters
        string? aggregation = null;
        string? groupBy = null;

        if (filters.ContainsKey("aggregation"))
        {
            aggregation = GetStringValue(filters["aggregation"]);
            filters.Remove("aggregation");
            _logger.LogInformation("Extracted aggregation: {Aggregation}", aggregation);
        }

        if (filters.ContainsKey("group_by"))
        {
            groupBy = GetStringValue(filters["group_by"]);
            filters.Remove("group_by");
            _logger.LogInformation("Extracted group_by: {GroupBy}", groupBy);
        }

        _logger.LogInformation("ProcessStructuredQuery START - Query: '{Query}', Aggregation: {Agg}, GroupBy: {Group}",
            query, aggregation ?? "none", groupBy ?? "none");

        // Step 1: Apply all filters to get filtered dataset
        var filterStart = sw.ElapsedMilliseconds;
        IEnumerable<IEXMarketData> data = _dataService.GetAllData();
        _logger.LogInformation("GetAllData took {Ms}ms", sw.ElapsedMilliseconds - filterStart);

        var applyFilterStart = sw.ElapsedMilliseconds;
        data = ApplyFilters(data, filters);
        _logger.LogInformation("ApplyFilters took {Ms}ms", sw.ElapsedMilliseconds - applyFilterStart);

        // Convert to list for aggregations (full filtered dataset)
        var toListStart = sw.ElapsedMilliseconds;
        var filteredData = data.ToList();
        _logger.LogInformation("ToList took {Ms}ms, Count: {Count}", sw.ElapsedMilliseconds - toListStart, filteredData.Count);

        // Step 2: Check if aggregation or grouping is requested
        if (!string.IsNullOrEmpty(aggregation) || !string.IsNullOrEmpty(groupBy))
        {
            _logger.LogInformation("Performing aggregation. Aggregation: {Agg}, GroupBy: {Group}", aggregation, groupBy);
            var result = PerformAggregation(filteredData, aggregation, groupBy, query);
            sw.Stop();
            _logger.LogInformation("ProcessStructuredQuery COMPLETE - Total time: {Ms}ms", sw.ElapsedMilliseconds);
            return result;
        }

        // Step 3: No aggregation - return raw data with limit applied
        var displayData = limit.HasValue ? filteredData.Take(limit.Value).ToList() : filteredData;

        sw.Stop();
        _logger.LogInformation("ProcessStructuredQuery COMPLETE (no aggregation) - Total time: {Ms}ms", sw.ElapsedMilliseconds);

        return new QueryResult
        {
            Success = true,
            Message = $"Found {filteredData.Count} records" + (limit.HasValue ? $" (showing first {displayData.Count})" : ""),
            Data = displayData,
            TotalRecords = filteredData.Count,
            Metadata = new Dictionary<string, object>
            {
                ["filtered_count"] = filteredData.Count,
                ["displayed_count"] = displayData.Count,
                ["has_more"] = limit.HasValue && filteredData.Count > limit.Value
            }
        };
    }

    /// <summary>
    /// Apply all filters to dataset
    /// </summary>
    private IEnumerable<IEXMarketData> ApplyFilters(IEnumerable<IEXMarketData> data, Dictionary<string, object> filters)
    {
        // Market type filter
        if (filters.ContainsKey("market_type"))
        {
            var marketType = GetStringValue(filters["market_type"]);
            if (!string.IsNullOrEmpty(marketType))
            {
                data = data.Where(d => d.Type.Equals(marketType, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Year filter
        if (filters.ContainsKey("year"))
        {
            var year = GetIntValue(filters["year"]);
            if (year.HasValue)
            {
                data = data.Where(d => d.Year == year.Value);
            }
        }

        // Month filter
        if (filters.ContainsKey("month"))
        {
            var month = GetIntValue(filters["month"]);
            if (month.HasValue)
            {
                data = data.Where(d => d.Date.Month == month.Value);
            }
        }

        // Day filter
        if (filters.ContainsKey("day"))
        {
            var day = GetIntValue(filters["day"]);
            if (day.HasValue)
            {
                data = data.Where(d => d.Date.Day == day.Value);
            }
        }

        // Date range filter
        if (filters.ContainsKey("start_date") && filters.ContainsKey("end_date"))
        {
            var startDateStr = GetStringValue(filters["start_date"]);
            var endDateStr = GetStringValue(filters["end_date"]);

            if (DateTime.TryParse(startDateStr, out var startDate) && DateTime.TryParse(endDateStr, out var endDate))
            {
                data = data.Where(d => d.Date >= startDate && d.Date <= endDate);
            }
        }

        // MCP range filters
        if (filters.ContainsKey("mcp_min"))
        {
            var mcpMin = GetDecimalValue(filters["mcp_min"]);
            if (mcpMin.HasValue)
            {
                data = data.Where(d => d.MCP >= mcpMin.Value);
            }
        }

        if (filters.ContainsKey("mcp_max"))
        {
            var mcpMax = GetDecimalValue(filters["mcp_max"]);
            if (mcpMax.HasValue)
            {
                data = data.Where(d => d.MCP <= mcpMax.Value);
            }
        }

        // MCV range filters
        if (filters.ContainsKey("mcv_min"))
        {
            var mcvMin = GetDecimalValue(filters["mcv_min"]);
            if (mcvMin.HasValue)
            {
                data = data.Where(d => d.MCV >= mcvMin.Value);
            }
        }

        if (filters.ContainsKey("mcv_max"))
        {
            var mcvMax = GetDecimalValue(filters["mcv_max"]);
            if (mcvMax.HasValue)
            {
                data = data.Where(d => d.MCV <= mcvMax.Value);
            }
        }

        // Time block filter (exact match)
        if (filters.ContainsKey("time_blocks") && filters["time_blocks"] is IEnumerable<object> timeBlocks)
        {
            var blockList = timeBlocks.Select(tb => GetStringValue(tb)).Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (blockList.Any())
            {
                data = data.Where(d => blockList.Contains(d.TimeBlock));
            }
        }

        // Time block range filter (handle midnight crossing)
        if (filters.ContainsKey("time_block_start") && filters.ContainsKey("time_block_end"))
        {
            var startTime = GetStringValue(filters["time_block_start"]);
            var endTime = GetStringValue(filters["time_block_end"]);

            if (!string.IsNullOrEmpty(startTime) && !string.IsNullOrEmpty(endTime))
            {
                // Check if time range crosses midnight (e.g., 21:00 to 06:00)
                if (string.Compare(startTime, endTime, StringComparison.Ordinal) > 0)
                {
                    // Time range crosses midnight: include times >= start OR <= end
                    data = data.Where(d =>
                    {
                        var timeBlockHour = d.TimeBlock.Split('-')[0];
                        return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 ||
                               string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                    });
                }
                else
                {
                    // Normal time range: include times >= start AND <= end
                    data = data.Where(d =>
                    {
                        var timeBlockHour = d.TimeBlock.Split('-')[0];
                        return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 &&
                               string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                    });
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Safely extract string value from object (handles JsonElement)
    /// </summary>
    private string GetStringValue(object value)
    {
        if (value == null) return string.Empty;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.GetString() ?? string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Safely extract int value from object (handles JsonElement)
    /// </summary>
    private int? GetIntValue(object value)
    {
        if (value == null) return null;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                return jsonElement.GetInt32();
            }

            var str = jsonElement.GetString();
            if (int.TryParse(str, out var intValue))
            {
                return intValue;
            }

            return null;
        }

        if (value is int intVal) return intVal;
        if (int.TryParse(value.ToString(), out var parsed)) return parsed;

        return null;
    }

    /// <summary>
    /// Safely extract decimal value from object (handles JsonElement)
    /// </summary>
    private decimal? GetDecimalValue(object value)
    {
        if (value == null) return null;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                return jsonElement.GetDecimal();
            }

            var str = jsonElement.GetString();
            if (decimal.TryParse(str, out var decimalValue))
            {
                return decimalValue;
            }

            return null;
        }

        if (value is decimal decVal) return decVal;
        if (value is double dblVal) return (decimal)dblVal;
        if (value is float fltVal) return (decimal)fltVal;
        if (value is int intVal) return intVal;
        if (decimal.TryParse(value.ToString(), out var parsed)) return parsed;

        return null;
    }

    /// <summary>
    /// Perform aggregation on filtered data
    /// </summary>
    private QueryResult PerformAggregation(List<IEXMarketData> filteredData, string? aggregation, string? groupBy, string originalQuery)
    {
        if (string.IsNullOrEmpty(groupBy))
        {
            // Simple aggregation (no grouping)
            return PerformSimpleAggregation(filteredData, aggregation, originalQuery);
        }
        else
        {
            // Grouped aggregation
            return PerformGroupedAggregation(filteredData, aggregation, groupBy, originalQuery);
        }
    }

    /// <summary>
    /// Perform simple aggregation (no grouping)
    /// </summary>
    private QueryResult PerformSimpleAggregation(List<IEXMarketData> data, string? aggregation, string query)
    {
        if (!data.Any())
        {
            return new QueryResult
            {
                Success = true,
                Message = "No data found matching filters",
                TotalRecords = 0,
                Data = new List<IEXMarketData>()
            };
        }

        var result = new Dictionary<string, object>
        {
            ["query"] = query,
            ["record_count"] = data.Count
        };

        switch (aggregation?.ToLowerInvariant())
        {
            case "count":
                result["count"] = data.Count;
                break;

            case "average":
                result["mcp_average"] = Math.Round(data.Average(d => d.MCP), 2);
                result["mcv_average"] = Math.Round(data.Average(d => d.MCV), 2);
                break;

            case "sum":
                result["mcp_sum"] = Math.Round(data.Sum(d => d.MCP), 2);
                result["mcv_sum"] = Math.Round(data.Sum(d => d.MCV), 2);
                break;

            case "max":
                result["mcp_max"] = data.Max(d => d.MCP);
                result["mcv_max"] = data.Max(d => d.MCV);
                var maxMcpRecord = data.First(d => d.MCP == data.Max(x => x.MCP));
                var maxMcvRecord = data.First(d => d.MCV == data.Max(x => x.MCV));
                result["mcp_max_date"] = maxMcpRecord.Date.ToString("yyyy-MM-dd");
                result["mcp_max_time_block"] = maxMcpRecord.TimeBlock;
                result["mcv_max_date"] = maxMcvRecord.Date.ToString("yyyy-MM-dd");
                result["mcv_max_time_block"] = maxMcvRecord.TimeBlock;
                break;

            case "min":
                result["mcp_min"] = data.Min(d => d.MCP);
                result["mcv_min"] = data.Min(d => d.MCV);
                var minMcpRecord = data.First(d => d.MCP == data.Min(x => x.MCP));
                var minMcvRecord = data.First(d => d.MCV == data.Min(x => x.MCV));
                result["mcp_min_date"] = minMcpRecord.Date.ToString("yyyy-MM-dd");
                result["mcp_min_time_block"] = minMcpRecord.TimeBlock;
                result["mcv_min_date"] = minMcvRecord.Date.ToString("yyyy-MM-dd");
                result["mcv_min_time_block"] = minMcvRecord.TimeBlock;
                break;

            case "stddev":
                result["mcp_stddev"] = Math.Round(CalculateStandardDeviation(data.Select(d => (double)d.MCP)), 2);
                result["mcv_stddev"] = Math.Round(CalculateStandardDeviation(data.Select(d => (double)d.MCV)), 2);
                result["mcp_average"] = Math.Round(data.Average(d => d.MCP), 2);
                result["mcv_average"] = Math.Round(data.Average(d => d.MCV), 2);
                break;

            default:
                // No specific aggregation, return summary stats
                result["mcp_average"] = Math.Round(data.Average(d => d.MCP), 2);
                result["mcv_average"] = Math.Round(data.Average(d => d.MCV), 2);
                result["mcp_max"] = data.Max(d => d.MCP);
                result["mcv_max"] = data.Max(d => d.MCV);
                result["mcp_min"] = data.Min(d => d.MCP);
                result["mcv_min"] = data.Min(d => d.MCV);
                break;
        }

        return new QueryResult
        {
            Success = true,
            Message = $"Aggregation complete: {aggregation ?? "summary"}",
            TotalRecords = data.Count,
            Metadata = result,
            Data = new List<IEXMarketData>() // No raw data for aggregations
        };
    }

    /// <summary>
    /// Perform grouped aggregation
    /// </summary>
    private QueryResult PerformGroupedAggregation(List<IEXMarketData> data, string? aggregation, string groupBy, string query)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("PerformGroupedAggregation START - Records: {Count}, GroupBy: {GroupBy}, Aggregation: {Agg}",
            data.Count, groupBy, aggregation);

        if (!data.Any())
        {
            return new QueryResult
            {
                Success = true,
                Message = "No data found matching filters",
                TotalRecords = 0,
                Data = new List<IEXMarketData>()
            };
        }

        var groupStart = sw.ElapsedMilliseconds;
        var groupedData = GroupData(data, groupBy);
        _logger.LogInformation("GroupData took {Ms}ms, Groups: {Count}", sw.ElapsedMilliseconds - groupStart, groupedData.Count);

        var aggregationResults = new List<Dictionary<string, object>>();

        var aggStart = sw.ElapsedMilliseconds;
        foreach (var group in groupedData.OrderBy(g => g.Key))
        {
            var groupResult = new Dictionary<string, object>
            {
                ["group_key"] = group.Key,
                ["record_count"] = group.Value.Count
            };

            // Apply aggregation to each group
            switch (aggregation?.ToLowerInvariant())
            {
                case "average":
                    groupResult["mcp_average"] = Math.Round(group.Value.Average(d => d.MCP), 2);
                    groupResult["mcv_average"] = Math.Round(group.Value.Average(d => d.MCV), 2);
                    break;

                case "sum":
                    groupResult["mcp_sum"] = Math.Round(group.Value.Sum(d => d.MCP), 2);
                    groupResult["mcv_sum"] = Math.Round(group.Value.Sum(d => d.MCV), 2);
                    break;

                case "max":
                    groupResult["mcp_max"] = group.Value.Max(d => d.MCP);
                    groupResult["mcv_max"] = group.Value.Max(d => d.MCV);
                    break;

                case "min":
                    groupResult["mcp_min"] = group.Value.Min(d => d.MCP);
                    groupResult["mcv_min"] = group.Value.Min(d => d.MCV);
                    break;

                case "stddev":
                    groupResult["mcp_stddev"] = Math.Round(CalculateStandardDeviation(group.Value.Select(d => (double)d.MCP)), 2);
                    groupResult["mcv_stddev"] = Math.Round(CalculateStandardDeviation(group.Value.Select(d => (double)d.MCV)), 2);
                    break;

                case "count":
                    // Already added record_count above
                    break;

                default:
                    // Default to average
                    groupResult["mcp_average"] = Math.Round(group.Value.Average(d => d.MCP), 2);
                    groupResult["mcv_average"] = Math.Round(group.Value.Average(d => d.MCV), 2);
                    break;
            }

            aggregationResults.Add(groupResult);
        }
        _logger.LogInformation("Aggregation calculations took {Ms}ms", sw.ElapsedMilliseconds - aggStart);

        var result = new QueryResult
        {
            Success = true,
            Message = $"Grouped by {groupBy} with {aggregation ?? "summary"} aggregation",
            TotalRecords = data.Count,
            Metadata = new Dictionary<string, object>
            {
                ["query"] = query,
                ["group_by"] = groupBy,
                ["aggregation"] = aggregation ?? "summary",
                ["group_count"] = aggregationResults.Count,
                ["total_records"] = data.Count,
                ["groups"] = aggregationResults
            },
            Data = new List<IEXMarketData>() // No raw data for aggregations
        };

        // Add chart-ready data
        var chartStart = sw.ElapsedMilliseconds;
        var chartData = FormatForCharts(result);
        if (chartData.Any())
        {
            result.Metadata["chart_data"] = chartData;
        }
        _logger.LogInformation("FormatForCharts took {Ms}ms", sw.ElapsedMilliseconds - chartStart);

        sw.Stop();
        _logger.LogInformation("PerformGroupedAggregation COMPLETE - Total time: {Ms}ms", sw.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Group data by specified field
    /// </summary>
    private Dictionary<string, List<IEXMarketData>> GroupData(List<IEXMarketData> data, string groupBy)
    {
        return groupBy.ToLowerInvariant() switch
        {
            "market_type" => data.GroupBy(d => d.Type).ToDictionary(g => g.Key, g => g.ToList()),
            "year" => data.GroupBy(d => d.Year.ToString()).ToDictionary(g => g.Key, g => g.ToList()),
            "month" => data.GroupBy(d => d.Date.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.ToList()),
            "date" => data.GroupBy(d => d.Date.ToString("yyyy-MM-dd")).ToDictionary(g => g.Key, g => g.ToList()),
            "time_block" => data.GroupBy(d => d.TimeBlock).ToDictionary(g => g.Key, g => g.ToList()),
            "hour" => data.GroupBy(d => d.TimeBlock.Split('-')[0].Split(':')[0]).ToDictionary(g => g.Key, g => g.ToList()),
            _ => new Dictionary<string, List<IEXMarketData>> { ["all"] = data }
        };
    }

    /// <summary>
    /// Get comprehensive statistics
    /// </summary>
    private async Task<MCPToolCallResponse> ExecuteGetStatistics(Dictionary<string, object>? arguments)
    {
        await Task.CompletedTask;

        string marketType = arguments != null && arguments.ContainsKey("market_type")
            ? arguments["market_type"].ToString()!
            : "ALL";

        var stats = marketType.ToUpperInvariant() == "ALL"
            ? _dataService.GetStatistics()
            : GetStatisticsByMarketType(marketType);

        return new MCPToolCallResponse
        {
            Success = true,
            Result = stats
        };
    }

    /// <summary>
    /// Get statistics filtered by market type
    /// </summary>
    private Dictionary<string, object> GetStatisticsByMarketType(string marketType)
    {
        var data = _dataService.GetDataByType(marketType).ToList();

        if (!data.Any())
        {
            return new Dictionary<string, object>
            {
                ["MarketType"] = marketType,
                ["TotalRecords"] = 0,
                ["Message"] = $"No data found for market type: {marketType}"
            };
        }

        return new Dictionary<string, object>
        {
            ["MarketType"] = marketType,
            ["TotalRecords"] = data.Count,
            ["MCP"] = new
            {
                Average = data.Average(d => d.MCP),
                Max = data.Max(d => d.MCP),
                Min = data.Min(d => d.MCP),
                StdDev = CalculateStandardDeviation(data.Select(d => (double)d.MCP))
            },
            ["MCV"] = new
            {
                Average = data.Average(d => d.MCV),
                Max = data.Max(d => d.MCV),
                Min = data.Min(d => d.MCV),
                StdDev = CalculateStandardDeviation(data.Select(d => (double)d.MCV))
            },
            ["DateRange"] = new
            {
                Start = data.Min(d => d.Date),
                End = data.Max(d => d.Date)
            }
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Calculate standard deviation
    /// </summary>
    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        if (!valuesList.Any()) return 0;

        var avg = valuesList.Average();
        var sumOfSquares = valuesList.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / valuesList.Count);
    }

    /// <summary>
    /// Format aggregation results for chart visualization
    /// </summary>
    private Dictionary<string, object> FormatForCharts(QueryResult result)
    {
        var chartData = new Dictionary<string, object>();

        if (result.Metadata == null || !result.Metadata.ContainsKey("groups"))
        {
            return chartData;
        }

        var groups = result.Metadata["groups"] as List<Dictionary<string, object>>;
        if (groups == null || !groups.Any())
        {
            return chartData;
        }

        // Extract labels and values for charts
        var labels = new List<string>();
        var mcpValues = new List<decimal>();
        var mcvValues = new List<decimal>();

        foreach (var group in groups)
        {
            labels.Add(group["group_key"]?.ToString() ?? "Unknown");

            // Get the first numeric value for MCP
            if (group.ContainsKey("mcp_average"))
                mcpValues.Add(Convert.ToDecimal(group["mcp_average"]));
            else if (group.ContainsKey("mcp_max"))
                mcpValues.Add(Convert.ToDecimal(group["mcp_max"]));
            else if (group.ContainsKey("mcp_sum"))
                mcpValues.Add(Convert.ToDecimal(group["mcp_sum"]));
            else
                mcpValues.Add(0);

            // Get the first numeric value for MCV
            if (group.ContainsKey("mcv_average"))
                mcvValues.Add(Convert.ToDecimal(group["mcv_average"]));
            else if (group.ContainsKey("mcv_max"))
                mcvValues.Add(Convert.ToDecimal(group["mcv_max"]));
            else if (group.ContainsKey("mcv_sum"))
                mcvValues.Add(Convert.ToDecimal(group["mcv_sum"]));
            else
                mcvValues.Add(0);
        }

        chartData["labels"] = labels;
        chartData["datasets"] = new[]
        {
            new
            {
                label = "MCP (Rs/kWh)",
                data = mcpValues,
                borderColor = "rgb(75, 192, 192)",
                backgroundColor = "rgba(75, 192, 192, 0.2)"
            },
            new
            {
                label = "MCV (GW)",
                data = mcvValues,
                borderColor = "rgb(255, 99, 132)",
                backgroundColor = "rgba(255, 99, 132, 0.2)"
            }
        };

        return chartData;
    }

    /// <summary>
    /// Generate heat map data for time blocks
    /// </summary>
    private Dictionary<string, object> GenerateHeatMapData(List<IEXMarketData> data, string metricType = "mcp")
    {
        var heatMapData = new Dictionary<string, object>();

        // Calculate date range in days
        var minDate = data.Min(d => d.Date);
        var maxDate = data.Max(d => d.Date);
        var daysDiff = (maxDate - minDate).Days;

        // Special handling for single day: Create 4 rows (minute slots) × 24 columns (hours)
        if (daysDiff == 0)
        {
            var minuteSlots = new List<string> { ":00", ":15", ":30", ":45" };
            var hours = Enumerable.Range(0, 24).Select(h => h.ToString("D2")).ToList();

            var singleDayMatrix = new List<List<decimal?>>();

            // Create 4 rows (one for each 15-minute slot within an hour)
            foreach (var minuteSlot in minuteSlots)
            {
                var row = new List<decimal?>();

                // Create 24 columns (one for each hour)
                foreach (var hour in hours)
                {
                    // Find matching time blocks (e.g., "08:15:00-08:30:00" matches hour "08" and minute ":15")
                    var matchingRecords = data.Where(d =>
                    {
                        var timeBlock = d.TimeBlock;
                        var startTime = timeBlock.Split('-')[0]; // Get "08:15:00"
                        return startTime.StartsWith(hour + minuteSlot); // Match "08:15"
                    }).ToList();

                    if (matchingRecords.Any())
                    {
                        var avgValue = metricType.ToLowerInvariant() == "mcv"
                            ? matchingRecords.Average(r => r.MCV)
                            : matchingRecords.Average(r => r.MCP);
                        row.Add(avgValue);
                    }
                    else
                    {
                        row.Add(null);
                    }
                }

                singleDayMatrix.Add(row);
            }

            heatMapData["dates"] = hours; // X-axis: Hours (00-23)
            heatMapData["time_blocks"] = minuteSlots; // Y-axis: Minute slots (:00, :15, :30, :45)
            heatMapData["matrix"] = singleDayMatrix;
            heatMapData["metric"] = metricType.ToUpperInvariant();
            heatMapData["grouping_unit"] = "hourly_15min";
            heatMapData["days_range"] = 0;

            return heatMapData;
        }

        // Group by appropriate time unit based on date range
        List<IGrouping<string, IEXMarketData>> groupedByDate;
        string groupingUnit;

        if (daysDiff <= 60)
        {
            // 0-60 days: Group by day
            groupedByDate = data.GroupBy(d => d.Date.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "day";
        }
        else if (daysDiff <= 181)
        {
            // 61-181 days (2-6 months): Group by week
            groupedByDate = data.GroupBy(d =>
            {
                var year = d.Date.Year;
                var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
                    .GetWeekOfYear(d.Date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                return $"{year}-W{weekOfYear:D2}";
            })
            .OrderBy(g => g.Key)
            .ToList();
            groupingUnit = "week";
        }
        else
        {
            // 182+ days: Group by month
            groupedByDate = data.GroupBy(d => d.Date.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "month";
        }

        var dates = groupedByDate.Select(g => g.Key).ToList();
        var timeBlocks = data.Select(d => d.TimeBlock).Distinct().OrderBy(tb => tb).ToList();

        // Build heat map matrix
        var matrix = new List<List<decimal?>>();

        foreach (var dateGroup in groupedByDate)
        {
            var row = new List<decimal?>();
            foreach (var timeBlock in timeBlocks)
            {
                var records = dateGroup.Where(d => d.TimeBlock == timeBlock).ToList();
                if (records.Any())
                {
                    // For weekly/monthly grouping, use average
                    var avgValue = metricType.ToLowerInvariant() == "mcv"
                        ? records.Average(r => r.MCV)
                        : records.Average(r => r.MCP);
                    row.Add(avgValue);
                }
                else
                {
                    row.Add(null);
                }
            }
            matrix.Add(row);
        }

        heatMapData["dates"] = dates;
        heatMapData["time_blocks"] = timeBlocks;
        heatMapData["matrix"] = matrix;
        heatMapData["metric"] = metricType.ToUpperInvariant();
        heatMapData["grouping_unit"] = groupingUnit;
        heatMapData["days_range"] = daysDiff;

        return heatMapData;
    }

    /// <summary>
    /// Calculate time series data for line charts
    /// </summary>
    private Dictionary<string, object> GenerateTimeSeriesData(List<IEXMarketData> data, string groupBy = "date")
    {
        var timeSeriesData = new Dictionary<string, object>();

        var grouped = groupBy.ToLowerInvariant() switch
        {
            "hour" => data.GroupBy(d => d.TimeBlock.Split('-')[0]).OrderBy(g => g.Key),
            "month" => data.GroupBy(d => d.Date.ToString("yyyy-MM")).OrderBy(g => g.Key),
            _ => data.GroupBy(d => d.Date.ToString("yyyy-MM-dd")).OrderBy(g => g.Key)
        };

        var labels = new List<string>();
        var mcpAvg = new List<decimal>();
        var mcvAvg = new List<decimal>();
        var mcpMax = new List<decimal>();
        var mcpMin = new List<decimal>();

        foreach (var group in grouped)
        {
            labels.Add(group.Key);
            mcpAvg.Add(Math.Round(group.Average(d => d.MCP), 2));
            mcvAvg.Add(Math.Round(group.Average(d => d.MCV), 2));
            mcpMax.Add(group.Max(d => d.MCP));
            mcpMin.Add(group.Min(d => d.MCP));
        }

        timeSeriesData["labels"] = labels;
        timeSeriesData["mcp_average"] = mcpAvg;
        timeSeriesData["mcv_average"] = mcvAvg;
        timeSeriesData["mcp_max"] = mcpMax;
        timeSeriesData["mcp_min"] = mcpMin;

        return timeSeriesData;
    }

    #endregion

    #region Direct Query Methods (for REST compatibility)

    /// <summary>
    /// Process natural language query (direct method)
    /// </summary>
    public async Task<QueryResult> QueryAsync(string query, int? limit = null)
    {
        _logger.LogInformation("MCP Server - Direct query: '{Query}'", query);
        return await _nlpService.ProcessQueryAsync(query, limit);
    }

    /// <summary>
    /// Get query suggestions
    /// </summary>
    public List<string> GetSuggestions(string? partialQuery = null)
    {
        return _nlpService.GetQuerySuggestions(partialQuery ?? string.Empty);
    }

    /// <summary>
    /// Get all statistics
    /// </summary>
    public Dictionary<string, object> GetStatistics()
    {
        return _dataService.GetStatistics();
    }

    /// <summary>
    /// Generate heat map data for visualization
    /// </summary>
    public Dictionary<string, object> GetHeatMapData(Dictionary<string, object>? filters = null, string metricType = "mcp")
    {
        IEnumerable<IEXMarketData> data = _dataService.GetAllData();

        // Log filters for debugging
        if (filters != null && filters.Any())
        {
            _logger.LogInformation("GetHeatMapData - Applying filters: {Filters}",
                string.Join(", ", filters.Select(f => $"{f.Key}={f.Value}")));
        }

        // Apply filters if provided
        if (filters != null && filters.Any())
        {
            data = ApplyFilters(data, filters);
        }

        var filteredData = data.ToList();

        _logger.LogInformation("GetHeatMapData - Filtered data count: {Count}", filteredData.Count);

        if (filteredData.Any())
        {
            var timeBlocks = filteredData.Select(d => d.TimeBlock).Distinct().OrderBy(tb => tb).ToList();
            _logger.LogInformation("GetHeatMapData - Time blocks in filtered data: {TimeBlocks}",
                string.Join(", ", timeBlocks.Take(10)));
        }

        if (!filteredData.Any())
        {
            return new Dictionary<string, object>
            {
                ["success"] = false,
                ["message"] = "No data found matching filters"
            };
        }

        var heatMapData = GenerateHeatMapData(filteredData, metricType);
        heatMapData["success"] = true;
        heatMapData["message"] = $"Heat map generated for {filteredData.Count} records";

        // Add time period information
        var startDate = filteredData.Min(d => d.Date);
        var endDate = filteredData.Max(d => d.Date);
        heatMapData["time_period_start"] = startDate.ToString("yyyy-MM-dd");
        heatMapData["time_period_end"] = endDate.ToString("yyyy-MM-dd");

        // Add market information
        var markets = filteredData.Select(d => d.Type).Distinct().ToList();
        heatMapData["markets"] = markets;

        return heatMapData;
    }

    #endregion
}
