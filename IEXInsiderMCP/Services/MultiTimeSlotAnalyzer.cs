using IEXInsiderMCP.Models;
using System.Text.RegularExpressions;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Analyzes data across multiple time slots for market comparison
/// </summary>
public class MultiTimeSlotAnalyzer
{
    private readonly IEXDataService _dataService;
    private readonly MCPServer _mcpServer;
    private readonly ILogger<MultiTimeSlotAnalyzer> _logger;

    public MultiTimeSlotAnalyzer(
        IEXDataService dataService,
        MCPServer mcpServer,
        ILogger<MultiTimeSlotAnalyzer> logger)
    {
        _dataService = dataService;
        _mcpServer = mcpServer;
        _logger = logger;
    }

    /// <summary>
    /// Parse natural language query to extract time slots, markets, and other parameters
    /// </summary>
    public MultiTimeSlotRequest ParseQuery(string query)
    {
        var request = new MultiTimeSlotRequest { Query = query };
        var queryLower = query.ToLower();

        // Extract metric types (MCP and/or MCV) with their chart types
        var hasMCV = queryLower.Contains("mcv") || queryLower.Contains("volume");
        var hasMCP = queryLower.Contains("mcp") || queryLower.Contains("price");

        // Check for specific chart type mentions per metric
        var barForMCV = Regex.IsMatch(queryLower, @"bar\s+(chart|graph)\s+(for|of)\s+(mcv|volume)");
        var lineForMCP = Regex.IsMatch(queryLower, @"line\s+(chart|graph)\s+(for|of)\s+(mcp|price)");
        var barForMCP = Regex.IsMatch(queryLower, @"bar\s+(chart|graph)\s+(for|of)\s+(mcp|price)");
        var lineForMCV = Regex.IsMatch(queryLower, @"line\s+(chart|graph)\s+(for|of)\s+(mcv|volume)");

        // If both metrics are mentioned, use the new lists
        if (hasMCV && hasMCP)
        {
            request.MetricTypes.Add("mcv");
            request.MetricTypes.Add("mcp");

            // Set chart types based on specific mentions
            if (barForMCV)
                request.MetricChartTypes["mcv"] = "bar";
            else if (lineForMCV)
                request.MetricChartTypes["mcv"] = "line";
            else
                request.MetricChartTypes["mcv"] = "bar"; // Default MCV to bar

            if (lineForMCP)
                request.MetricChartTypes["mcp"] = "line";
            else if (barForMCP)
                request.MetricChartTypes["mcp"] = "bar";
            else
                request.MetricChartTypes["mcp"] = "line"; // Default MCP to line
        }
        else
        {
            // Single metric (backwards compatibility)
            if (hasMCV)
                request.MetricType = "mcv";
            else
                request.MetricType = "mcp";

            // Extract chart type
            if (queryLower.Contains("bar chart") || queryLower.Contains("bar graph"))
                request.ChartType = "bar";
            else if (queryLower.Contains("line chart") || queryLower.Contains("line graph"))
                request.ChartType = "line";
            else if (queryLower.Contains("heat map") || queryLower.Contains("heatmap"))
                request.ChartType = "heatmap";
        }

        // Extract markets
        request.Markets = new List<MarketType>();
        if (queryLower.Contains("all") && queryLower.Contains("market"))
        {
            request.Markets = new List<MarketType> { MarketType.DAM, MarketType.GDAM, MarketType.RTM };
        }
        else
        {
            if (queryLower.Contains("dam") && !queryLower.Contains("gdam"))
                request.Markets.Add(MarketType.DAM);
            if (queryLower.Contains("gdam") || queryLower.Contains("g-dam"))
                request.Markets.Add(MarketType.GDAM);
            if (queryLower.Contains("rtm"))
                request.Markets.Add(MarketType.RTM);
        }

        // If no markets specified, default to all
        if (!request.Markets.Any())
            request.Markets = new List<MarketType> { MarketType.DAM, MarketType.GDAM, MarketType.RTM };

        // Extract date information
        var dateInfo = DateTimeExtractor.ExtractDateTimeInfo(query);
        if (dateInfo.Year.HasValue)
            request.Year = dateInfo.Year.Value;
        if (dateInfo.Month.HasValue)
            request.Month = GetMonthName(dateInfo.Month.Value);

        // Extract quarter information (Q1, Q2, Q3, Q4)
        // For quarters, we DON'T set the Month filter - instead we let the analysis
        // include all months in the year, which will cover the entire quarter
        var quarterMatch = Regex.Match(queryLower, @"\bq([1-4])\b");
        if (quarterMatch.Success && string.IsNullOrEmpty(request.Month))
        {
            var quarter = int.Parse(quarterMatch.Groups[1].Value);
            _logger.LogInformation("Detected quarter Q{Quarter}, will include all data for the year (no month filter)", quarter);
            // Note: Not setting request.Month so it includes all months for the year
            // This way Q1 includes Jan, Feb, Mar data
        }

        // If no year specified, default to current year
        if (!request.Year.HasValue)
        {
            request.Year = DateTime.Now.Year;
            _logger.LogInformation("No year specified, defaulting to {Year}", request.Year);
        }

        // Extract time slots from query
        request.TimeSlots = ExtractTimeSlots(query);

        _logger.LogInformation("Parsed query: Year={Year}, Month={Month}, TimeSlots={TimeSlotCount}, Markets={MarketCount}, MetricTypes={MetricTypes}",
            request.Year, request.Month, request.TimeSlots.Count, request.Markets.Count, string.Join(",", request.MetricTypes));

        return request;
    }

    /// <summary>
    /// Extract time slot definitions from query
    /// </summary>
    private List<TimeSlotDefinition> ExtractTimeSlots(string query)
    {
        var timeSlots = new List<TimeSlotDefinition>();

        // Pattern: "9AM to 5PM", "5 PM to 9PM", "9pm to 6am", "6am to 9am"
        var timeSlotPattern = @"(\d{1,2})\s*([ap]m)\s+to\s+(\d{1,2})\s*([ap]m)";
        var matches = Regex.Matches(query, timeSlotPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var startHour = int.Parse(match.Groups[1].Value);
            var startPeriod = match.Groups[2].Value.ToLower();
            var endHour = int.Parse(match.Groups[3].Value);
            var endPeriod = match.Groups[4].Value.ToLower();

            // Convert to 24-hour format
            if (startPeriod == "pm" && startHour < 12)
                startHour += 12;
            else if (startPeriod == "am" && startHour == 12)
                startHour = 0;

            if (endPeriod == "pm" && endHour < 12)
                endHour += 12;
            else if (endPeriod == "am" && endHour == 12)
                endHour = 0;

            var startTime = $"{startHour:D2}:00";
            var endTime = $"{endHour:D2}:00";

            var slotName = $"{match.Groups[1].Value}{match.Groups[2].Value.ToUpper()}-{match.Groups[3].Value}{match.Groups[4].Value.ToUpper()}";

            timeSlots.Add(new TimeSlotDefinition
            {
                Name = slotName,
                StartTime = startTime,
                EndTime = endTime
            });
        }

        return timeSlots;
    }

    /// <summary>
    /// Analyze data across multiple time slots and markets
    /// </summary>
    public async Task<MultiTimeSlotResponse> AnalyzeAsync(MultiTimeSlotRequest request)
    {
        _logger.LogInformation("Analyzing multi-time-slot request: {Query}", request.Query);

        // Check if this is a combined MCP+MCV request
        var isCombinedMetrics = request.MetricTypes.Count == 2;

        // Create appropriate message based on chart type
        string chartDescription;
        if (isCombinedMetrics)
        {
            var mcvType = request.MetricChartTypes.ContainsKey("mcv") ? request.MetricChartTypes["mcv"] : "bar";
            var mcpType = request.MetricChartTypes.ContainsKey("mcp") ? request.MetricChartTypes["mcp"] : "line";
            chartDescription = $"combined chart ({mcvType} chart for MCV + {mcpType} graph for MCP)";
        }
        else
        {
            chartDescription = request.ChartType;
        }

        var response = new MultiTimeSlotResponse
        {
            Success = true,
            Message = isCombinedMetrics
                ? $"Generated {chartDescription} for all {request.Markets.Count} markets across {request.TimeSlots.Count} time slot(s)"
                : $"Generated {chartDescription} for {request.Markets.Count} markets across {request.TimeSlots.Count} time slot(s)"
        };

        if (isCombinedMetrics)
        {
            // For combined metrics, create ONE chart per time slot showing all markets
            foreach (var timeSlot in request.TimeSlots)
            {
                try
                {
                    var result = await AnalyzeTimeSlotForAllMarkets(request.Markets.ToList(), timeSlot, request);
                    response.Results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing time slot {TimeSlot} for all markets", timeSlot.Name);
                }
            }
        }
        else
        {
            // For single metric, create separate charts per market
            foreach (var market in request.Markets)
            {
                foreach (var timeSlot in request.TimeSlots)
                {
                    try
                    {
                        var result = await AnalyzeTimeSlotForMarket(market, timeSlot, request);
                        response.Results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error analyzing time slot {TimeSlot} for market {Market}", timeSlot.Name, market);
                    }
                }
            }
        }

        return response;
    }

    /// <summary>
    /// Analyze a specific time slot for a specific market
    /// </summary>
    private async Task<TimeSlotResult> AnalyzeTimeSlotForMarket(
        MarketType market,
        TimeSlotDefinition timeSlot,
        MultiTimeSlotRequest request)
    {
        var marketString = market.ToString();

        // Build filters
        var filters = new Dictionary<string, object>
        {
            ["market_type"] = marketString,
            ["time_block_start"] = timeSlot.StartTime,
            ["time_block_end"] = timeSlot.EndTime
        };

        if (request.Year.HasValue)
            filters["year"] = request.Year.Value;

        if (!string.IsNullOrEmpty(request.Month))
        {
            var monthNumber = GetMonthNumber(request.Month);
            if (monthNumber.HasValue)
                filters["month"] = monthNumber.Value;
        }

        // Get data based on chart type
        Dictionary<string, object> chartData;

        // Check if both metrics are requested (combined chart)
        if (request.MetricTypes.Count == 2)
        {
            // Create combined chart with both MCP and MCV
            chartData = await GetCombinedChartDataAsync(filters, request.MetricTypes, request.MetricChartTypes);
        }
        else if (request.ChartType == "heatmap")
        {
            chartData = _mcpServer.GetHeatMapData(filters, request.MetricType);
        }
        else
        {
            // For bar/line charts, get aggregated data
            chartData = await GetAggregatedChartDataAsync(filters, request.MetricType, request.ChartType);
        }

        // Calculate statistics
        var data = _dataService.GetAllData()
            .Where(d => d.Type.Equals(marketString, StringComparison.OrdinalIgnoreCase));

        if (request.Year.HasValue)
            data = data.Where(d => d.Year == request.Year.Value);

        if (!string.IsNullOrEmpty(request.Month))
        {
            var monthNumber = GetMonthNumber(request.Month);
            if (monthNumber.HasValue)
                data = data.Where(d => d.Date.Month == monthNumber.Value);
        }

        // Apply time block filter (handle midnight crossing)
        data = data.Where(d =>
        {
            var timeBlockHour = d.TimeBlock.Split('-')[0];

            // Check if time range crosses midnight (e.g., 21:00 to 06:00)
            if (string.Compare(timeSlot.StartTime, timeSlot.EndTime, StringComparison.Ordinal) > 0)
            {
                // Time range crosses midnight: include times >= start OR <= end
                return string.Compare(timeBlockHour, timeSlot.StartTime, StringComparison.Ordinal) >= 0 ||
                       string.Compare(timeBlockHour, timeSlot.EndTime, StringComparison.Ordinal) <= 0;
            }
            else
            {
                // Normal time range: include times >= start AND <= end
                return string.Compare(timeBlockHour, timeSlot.StartTime, StringComparison.Ordinal) >= 0 &&
                       string.Compare(timeBlockHour, timeSlot.EndTime, StringComparison.Ordinal) <= 0;
            }
        });

        var dataList = data.ToList();

        var statistics = new Dictionary<string, decimal>();
        if (dataList.Any())
        {
            if (request.MetricType == "mcp")
            {
                statistics["average"] = Math.Round(dataList.Average(d => d.MCP), 2);
                statistics["max"] = Math.Round(dataList.Max(d => d.MCP), 2);
                statistics["min"] = Math.Round(dataList.Min(d => d.MCP), 2);
            }
            else
            {
                statistics["average"] = Math.Round(dataList.Average(d => d.MCV), 2);
                statistics["max"] = Math.Round(dataList.Max(d => d.MCV), 2);
                statistics["min"] = Math.Round(dataList.Min(d => d.MCV), 2);
            }
        }

        return new TimeSlotResult
        {
            TimeSlotName = timeSlot.Name,
            Market = marketString,
            RecordCount = dataList.Count,
            ChartData = chartData,
            Statistics = statistics
        };
    }

    /// <summary>
    /// Analyze a specific time slot for ALL markets (combined chart)
    /// </summary>
    private async Task<TimeSlotResult> AnalyzeTimeSlotForAllMarkets(
        List<MarketType> markets,
        TimeSlotDefinition timeSlot,
        MultiTimeSlotRequest request)
    {
        // Build filters for time slot
        var filters = new Dictionary<string, object>
        {
            ["time_block_start"] = timeSlot.StartTime,
            ["time_block_end"] = timeSlot.EndTime
        };

        if (request.Year.HasValue)
            filters["year"] = request.Year.Value;

        if (!string.IsNullOrEmpty(request.Month))
        {
            var monthNumber = GetMonthNumber(request.Month);
            if (monthNumber.HasValue)
                filters["month"] = monthNumber.Value;
        }

        // Get combined chart data for all markets
        var chartData = await GetCombinedChartDataForAllMarketsAsync(
            filters,
            markets,
            request.MetricTypes,
            request.MetricChartTypes);

        // Calculate statistics across all markets
        var allData = _dataService.GetAllData()
            .Where(d => markets.Any(m => d.Type.Equals(m.ToString(), StringComparison.OrdinalIgnoreCase)));

        if (request.Year.HasValue)
            allData = allData.Where(d => d.Year == request.Year.Value);

        if (!string.IsNullOrEmpty(request.Month))
        {
            var monthNumber = GetMonthNumber(request.Month);
            if (monthNumber.HasValue)
                allData = allData.Where(d => d.Date.Month == monthNumber.Value);
        }

        // Apply time block filter
        allData = allData.Where(d =>
        {
            var timeBlockHour = d.TimeBlock.Split('-')[0];
            if (string.Compare(timeSlot.StartTime, timeSlot.EndTime, StringComparison.Ordinal) > 0)
            {
                return string.Compare(timeBlockHour, timeSlot.StartTime, StringComparison.Ordinal) >= 0 ||
                       string.Compare(timeBlockHour, timeSlot.EndTime, StringComparison.Ordinal) <= 0;
            }
            else
            {
                return string.Compare(timeBlockHour, timeSlot.StartTime, StringComparison.Ordinal) >= 0 &&
                       string.Compare(timeBlockHour, timeSlot.EndTime, StringComparison.Ordinal) <= 0;
            }
        });

        var dataList = allData.ToList();

        var statistics = new Dictionary<string, decimal>();
        if (dataList.Any())
        {
            // Combined statistics for both metrics
            statistics["avg_mcp"] = Math.Round(dataList.Average(d => d.MCP), 2);
            statistics["max_mcp"] = Math.Round(dataList.Max(d => d.MCP), 2);
            statistics["min_mcp"] = Math.Round(dataList.Min(d => d.MCP), 2);
            statistics["avg_mcv"] = Math.Round(dataList.Average(d => d.MCV), 2);
            statistics["max_mcv"] = Math.Round(dataList.Max(d => d.MCV), 2);
            statistics["min_mcv"] = Math.Round(dataList.Min(d => d.MCV), 2);
        }

        return new TimeSlotResult
        {
            TimeSlotName = timeSlot.Name,
            Market = "All Markets",
            RecordCount = dataList.Count,
            ChartData = chartData,
            Statistics = statistics
        };
    }

    /// <summary>
    /// Get aggregated chart data for bar/line charts
    /// </summary>
    private async Task<Dictionary<string, object>> GetAggregatedChartDataAsync(
        Dictionary<string, object> filters,
        string metricType,
        string chartType)
    {
        await Task.CompletedTask;

        var data = _dataService.GetAllData();

        // Apply filters
        if (filters.ContainsKey("market_type"))
        {
            var marketType = filters["market_type"].ToString();
            data = data.Where(d => d.Type.Equals(marketType, StringComparison.OrdinalIgnoreCase));
        }

        if (filters.ContainsKey("year") && filters["year"] is int year)
        {
            data = data.Where(d => d.Year == year);
        }

        if (filters.ContainsKey("month") && filters["month"] is int month)
        {
            data = data.Where(d => d.Date.Month == month);
        }

        // Apply time block range filter (handle midnight crossing)
        if (filters.ContainsKey("time_block_start") && filters.ContainsKey("time_block_end"))
        {
            var startTime = filters["time_block_start"].ToString();
            var endTime = filters["time_block_end"].ToString();

            data = data.Where(d =>
            {
                var timeBlockHour = d.TimeBlock.Split('-')[0];

                // Check if time range crosses midnight (e.g., 21:00 to 06:00)
                if (string.Compare(startTime, endTime, StringComparison.Ordinal) > 0)
                {
                    // Time range crosses midnight: include times >= start OR <= end
                    return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 ||
                           string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                }
                else
                {
                    // Normal time range: include times >= start AND <= end
                    return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 &&
                           string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                }
            });
        }

        var dataList = data.ToList();

        if (!dataList.Any())
        {
            return new Dictionary<string, object>
            {
                ["success"] = true,
                ["chart_type"] = chartType,
                ["labels"] = new List<string>(),
                ["values"] = new List<decimal>()
            };
        }

        // Calculate date range and determine grouping
        var minDate = dataList.Min(d => d.Date);
        var maxDate = dataList.Max(d => d.Date);
        var daysDiff = (maxDate - minDate).Days;

        List<IGrouping<string, IEXMarketData>> groupedData;
        string groupingUnit;
        List<string> labels;
        List<decimal> values;

        if (daysDiff == 0)
        {
            // Single day: Group by time block (15-minute intervals)
            groupedData = dataList
                .GroupBy(d => d.TimeBlock)
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "time_block";

            labels = groupedData.Select(g => g.Key).ToList();
            values = groupedData.Select(g =>
            {
                if (metricType == "mcp")
                    return (decimal)g.Average(d => d.MCP);
                else
                    return (decimal)g.Average(d => d.MCV);
            }).ToList();
        }
        else if (daysDiff <= 60)
        {
            // 1-60 days: Group by day
            groupedData = dataList
                .GroupBy(d => d.Date.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "day";

            labels = groupedData.Select(g => g.Key).ToList();
            values = groupedData.Select(g =>
            {
                if (metricType == "mcp")
                    return (decimal)g.Average(d => d.MCP);
                else
                    return (decimal)g.Average(d => d.MCV);
            }).ToList();
        }
        else if (daysDiff <= 181)
        {
            // 61-181 days: Group by week
            groupedData = dataList
                .GroupBy(d =>
                {
                    var year = d.Date.Year;
                    var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
                        .GetWeekOfYear(d.Date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                    return $"{year}-W{weekOfYear:D2}";
                })
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "week";

            labels = groupedData.Select(g => g.Key).ToList();
            values = groupedData.Select(g =>
            {
                if (metricType == "mcp")
                    return (decimal)g.Average(d => d.MCP);
                else
                    return (decimal)g.Average(d => d.MCV);
            }).ToList();
        }
        else
        {
            // 182+ days: Group by month
            groupedData = dataList
                .GroupBy(d => d.Date.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "month";

            labels = groupedData.Select(g => g.Key).ToList();
            values = groupedData.Select(g =>
            {
                if (metricType == "mcp")
                    return (decimal)g.Average(d => d.MCP);
                else
                    return (decimal)g.Average(d => d.MCV);
            }).ToList();
        }

        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["chart_type"] = chartType,
            ["labels"] = labels,
            ["values"] = values,
            ["metric"] = metricType.ToUpper(),
            ["record_count"] = dataList.Count,
            ["grouping_unit"] = groupingUnit,
            ["days_range"] = daysDiff
        };
    }

    /// <summary>
    /// Get combined chart data for both MCP and MCV with dual Y-axes
    /// </summary>
    private async Task<Dictionary<string, object>> GetCombinedChartDataAsync(
        Dictionary<string, object> filters,
        List<string> metricTypes,
        Dictionary<string, string> metricChartTypes)
    {
        await Task.CompletedTask;

        var data = _dataService.GetAllData();

        // Apply filters
        if (filters.ContainsKey("market_type"))
        {
            var marketType = filters["market_type"].ToString();
            data = data.Where(d => d.Type.Equals(marketType, StringComparison.OrdinalIgnoreCase));
        }

        if (filters.ContainsKey("year") && filters["year"] is int year)
        {
            data = data.Where(d => d.Year == year);
        }

        if (filters.ContainsKey("month") && filters["month"] is int month)
        {
            data = data.Where(d => d.Date.Month == month);
        }

        // Apply time block range filter (handle midnight crossing)
        if (filters.ContainsKey("time_block_start") && filters.ContainsKey("time_block_end"))
        {
            var startTime = filters["time_block_start"].ToString();
            var endTime = filters["time_block_end"].ToString();

            data = data.Where(d =>
            {
                var timeBlockHour = d.TimeBlock.Split('-')[0];

                // Check if time range crosses midnight (e.g., 21:00 to 06:00)
                if (string.Compare(startTime, endTime, StringComparison.Ordinal) > 0)
                {
                    // Time range crosses midnight: include times >= start OR <= end
                    return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 ||
                           string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                }
                else
                {
                    // Normal time range: include times >= start AND <= end
                    return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 &&
                           string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                }
            });
        }

        var dataList = data.ToList();

        if (!dataList.Any())
        {
            return new Dictionary<string, object>
            {
                ["success"] = true,
                ["chart_type"] = "combined",
                ["labels"] = new List<string>(),
                ["datasets"] = new List<object>()
            };
        }

        // Calculate date range and determine grouping
        var minDate = dataList.Min(d => d.Date);
        var maxDate = dataList.Max(d => d.Date);
        var daysDiff = (maxDate - minDate).Days;

        List<IGrouping<string, IEXMarketData>> groupedData;
        string groupingUnit;
        List<string> labels;
        List<decimal> mcpValues;
        List<decimal> mcvValues;

        if (daysDiff == 0)
        {
            // Single day: Group by time block (15-minute intervals)
            groupedData = dataList
                .GroupBy(d => d.TimeBlock)
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "time_block";

            labels = groupedData.Select(g => g.Key).ToList();
            mcpValues = groupedData.Select(g => (decimal)g.Average(d => d.MCP)).ToList();
            mcvValues = groupedData.Select(g => (decimal)g.Average(d => d.MCV)).ToList();
        }
        else if (daysDiff <= 60)
        {
            // 1-60 days: Group by day
            groupedData = dataList
                .GroupBy(d => d.Date.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "day";

            labels = groupedData.Select(g => g.Key).ToList();
            mcpValues = groupedData.Select(g => (decimal)g.Average(d => d.MCP)).ToList();
            mcvValues = groupedData.Select(g => (decimal)g.Average(d => d.MCV)).ToList();
        }
        else if (daysDiff <= 181)
        {
            // 61-181 days: Group by week
            groupedData = dataList
                .GroupBy(d =>
                {
                    var year = d.Date.Year;
                    var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
                        .GetWeekOfYear(d.Date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                    return $"{year}-W{weekOfYear:D2}";
                })
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "week";

            labels = groupedData.Select(g => g.Key).ToList();
            mcpValues = groupedData.Select(g => (decimal)g.Average(d => d.MCP)).ToList();
            mcvValues = groupedData.Select(g => (decimal)g.Average(d => d.MCV)).ToList();
        }
        else
        {
            // 182+ days: Group by month
            groupedData = dataList
                .GroupBy(d => d.Date.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToList();
            groupingUnit = "month";

            labels = groupedData.Select(g => g.Key).ToList();
            mcpValues = groupedData.Select(g => (decimal)g.Average(d => d.MCP)).ToList();
            mcvValues = groupedData.Select(g => (decimal)g.Average(d => d.MCV)).ToList();
        }

        // Get chart types for each metric
        var mcpChartType = metricChartTypes.ContainsKey("mcp") ? metricChartTypes["mcp"] : "line";
        var mcvChartType = metricChartTypes.ContainsKey("mcv") ? metricChartTypes["mcv"] : "bar";

        // Create datasets for both metrics
        var datasets = new List<object>();

        // MCV dataset (typically bar)
        datasets.Add(new
        {
            label = "MCV (Volume)",
            data = mcvValues,
            type = mcvChartType,
            backgroundColor = "rgba(0, 168, 204, 0.8)",      // Accent Cyan
            borderColor = "rgba(0, 168, 204, 1)",
            borderWidth = 2,
            yAxisID = "y-mcv",
            order = 2  // Draw bars first (behind lines)
        });

        // MCP dataset (typically line)
        datasets.Add(new
        {
            label = "MCP (Price)",
            data = mcpValues,
            type = mcpChartType,
            backgroundColor = "rgba(15, 76, 129, 0.2)",      // Primary Blue with transparency
            borderColor = "rgba(15, 76, 129, 1)",
            borderWidth = 3,
            fill = false,
            tension = 0.4m,
            yAxisID = "y-mcp",
            order = 1  // Draw lines on top
        });

        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["chart_type"] = "combined",
            ["labels"] = labels,
            ["datasets"] = datasets,
            ["record_count"] = dataList.Count,
            ["grouping_unit"] = groupingUnit,
            ["days_range"] = daysDiff,
            ["has_dual_axes"] = true
        };
    }

    /// <summary>
    /// Get combined chart data for ALL markets with MCP and MCV
    /// </summary>
    private async Task<Dictionary<string, object>> GetCombinedChartDataForAllMarketsAsync(
        Dictionary<string, object> filters,
        List<MarketType> markets,
        List<string> metricTypes,
        Dictionary<string, string> metricChartTypes)
    {
        await Task.CompletedTask;

        // Market colors matching the theme
        var marketColors = new Dictionary<string, (string bg, string border)>
        {
            ["DAM"] = ("rgba(15, 76, 129, 0.8)", "rgba(15, 76, 129, 1)"),     // Primary Blue
            ["GDAM"] = ("rgba(0, 168, 204, 0.8)", "rgba(0, 168, 204, 1)"),    // Accent Cyan
            ["RTM"] = ("rgba(20, 184, 166, 0.8)", "rgba(20, 184, 166, 1)")    // Teal
        };

        // Get year/month filters
        int? year = filters.ContainsKey("year") && filters["year"] is int y ? y : null;
        int? month = filters.ContainsKey("month") && filters["month"] is int m ? m : null;
        string startTime = filters["time_block_start"].ToString();
        string endTime = filters["time_block_end"].ToString();

        // Collect data for each market
        var marketData = new Dictionary<string, (List<decimal> mcpValues, List<decimal> mcvValues)>();
        List<string> labels = null;
        string groupingUnit = "";
        int daysDiff = 0;

        foreach (var market in markets)
        {
            var data = _dataService.GetAllData()
                .Where(d => d.Type.Equals(market.ToString(), StringComparison.OrdinalIgnoreCase));

            if (year.HasValue)
                data = data.Where(d => d.Year == year.Value);

            if (month.HasValue)
                data = data.Where(d => d.Date.Month == month.Value);

            // Apply time block filter
            data = data.Where(d =>
            {
                var timeBlockHour = d.TimeBlock.Split('-')[0];
                if (string.Compare(startTime, endTime, StringComparison.Ordinal) > 0)
                {
                    return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 ||
                           string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                }
                else
                {
                    return string.Compare(timeBlockHour, startTime, StringComparison.Ordinal) >= 0 &&
                           string.Compare(timeBlockHour, endTime, StringComparison.Ordinal) <= 0;
                }
            });

            var dataList = data.ToList();

            if (!dataList.Any())
                continue;

            // Calculate date range and grouping (only once)
            if (labels == null)
            {
                var minDate = dataList.Min(d => d.Date);
                var maxDate = dataList.Max(d => d.Date);
                daysDiff = (maxDate - minDate).Days;

                List<IGrouping<string, IEXMarketData>> groupedByDate;

                if (daysDiff <= 60)
                {
                    groupedByDate = dataList
                        .GroupBy(d => d.Date.ToString("yyyy-MM-dd"))
                        .OrderBy(g => g.Key)
                        .ToList();
                    groupingUnit = "day";
                }
                else if (daysDiff <= 181)
                {
                    groupedByDate = dataList
                        .GroupBy(d =>
                        {
                            var year_val = d.Date.Year;
                            var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
                                .GetWeekOfYear(d.Date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                            return $"{year_val}-W{weekOfYear:D2}";
                        })
                        .OrderBy(g => g.Key)
                        .ToList();
                    groupingUnit = "week";
                }
                else
                {
                    groupedByDate = dataList
                        .GroupBy(d => d.Date.ToString("yyyy-MM"))
                        .OrderBy(g => g.Key)
                        .ToList();
                    groupingUnit = "month";
                }

                labels = groupedByDate.Select(g => g.Key).ToList();

                // Calculate values for this market
                var mcpValues = groupedByDate.Select(g => (decimal)g.Average(d => d.MCP)).ToList();
                var mcvValues = groupedByDate.Select(g => (decimal)g.Average(d => d.MCV)).ToList();

                marketData[market.ToString()] = (mcpValues, mcvValues);
            }
            else
            {
                // Reuse the same grouping logic for other markets
                List<IGrouping<string, IEXMarketData>> groupedByDate;

                if (daysDiff <= 60)
                {
                    groupedByDate = dataList
                        .GroupBy(d => d.Date.ToString("yyyy-MM-dd"))
                        .OrderBy(g => g.Key)
                        .ToList();
                }
                else if (daysDiff <= 181)
                {
                    groupedByDate = dataList
                        .GroupBy(d =>
                        {
                            var year_val = d.Date.Year;
                            var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
                                .GetWeekOfYear(d.Date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                            return $"{year_val}-W{weekOfYear:D2}";
                        })
                        .OrderBy(g => g.Key)
                        .ToList();
                }
                else
                {
                    groupedByDate = dataList
                        .GroupBy(d => d.Date.ToString("yyyy-MM"))
                        .OrderBy(g => g.Key)
                        .ToList();
                }

                var mcpValues = groupedByDate.Select(g => (decimal)g.Average(d => d.MCP)).ToList();
                var mcvValues = groupedByDate.Select(g => (decimal)g.Average(d => d.MCV)).ToList();

                marketData[market.ToString()] = (mcpValues, mcvValues);
            }
        }

        if (labels == null || !labels.Any())
        {
            return new Dictionary<string, object>
            {
                ["success"] = true,
                ["chart_type"] = "combined",
                ["labels"] = new List<string>(),
                ["datasets"] = new List<object>(),
                ["has_dual_axes"] = true
            };
        }

        // Get chart types for each metric
        var mcvChartType = metricChartTypes.ContainsKey("mcv") ? metricChartTypes["mcv"] : "bar";
        var mcpChartType = metricChartTypes.ContainsKey("mcp") ? metricChartTypes["mcp"] : "line";

        // Create datasets for each market
        var datasets = new List<object>();

        // MCV datasets (bars) - one per market
        foreach (var market in markets)
        {
            var marketKey = market.ToString();
            if (!marketData.ContainsKey(marketKey))
                continue;

            var colors = marketColors[marketKey];
            datasets.Add(new
            {
                label = $"{marketKey} - MCV (Volume)",
                data = marketData[marketKey].mcvValues,
                type = mcvChartType,
                backgroundColor = colors.bg,
                borderColor = colors.border,
                borderWidth = 2,
                yAxisID = "y-mcv",
                order = 2  // Draw bars first
            });
        }

        // Line colors for MCP - distinct from bar colors for better visibility
        var lineColors = new Dictionary<string, (string bg, string border)>
        {
            ["DAM"] = ("rgba(239, 68, 68, 0.2)", "rgba(239, 68, 68, 1)"),      // Red
            ["GDAM"] = ("rgba(168, 85, 247, 0.2)", "rgba(168, 85, 247, 1)"),   // Purple
            ["RTM"] = ("rgba(34, 197, 94, 0.2)", "rgba(34, 197, 94, 1)")       // Green
        };

        // MCP datasets (lines) - one per market
        foreach (var market in markets)
        {
            var marketKey = market.ToString();
            if (!marketData.ContainsKey(marketKey))
                continue;

            var lineColor = lineColors[marketKey];
            datasets.Add(new
            {
                label = $"{marketKey} - MCP (Price)",
                data = marketData[marketKey].mcpValues,
                type = mcpChartType,
                backgroundColor = lineColor.bg,
                borderColor = lineColor.border,
                borderWidth = 3,
                fill = false,
                tension = 0.4m,
                yAxisID = "y-mcp",
                order = 1,  // Draw lines on top
                pointRadius = 4,
                pointHoverRadius = 6,
                pointBackgroundColor = lineColor.border,
                pointBorderColor = "#fff",
                pointBorderWidth = 2
            });
        }

        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["chart_type"] = "combined_multi_market",
            ["labels"] = labels,
            ["datasets"] = datasets,
            ["record_count"] = marketData.Values.Sum(v => v.mcpValues.Count),
            ["grouping_unit"] = groupingUnit,
            ["days_range"] = daysDiff,
            ["has_dual_axes"] = true,
            ["market_count"] = markets.Count
        };
    }

    private string GetMonthName(int month)
    {
        return month switch
        {
            1 => "January",
            2 => "February",
            3 => "March",
            4 => "April",
            5 => "May",
            6 => "June",
            7 => "July",
            8 => "August",
            9 => "September",
            10 => "October",
            11 => "November",
            12 => "December",
            _ => ""
        };
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
