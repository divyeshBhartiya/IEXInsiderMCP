using IEXInsiderMCP.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Natural language processing engine for generating intelligent, conversational responses
/// </summary>
public class NaturalLanguageEngine
{
    private readonly IEXDataService _dataService;
    private readonly InsightsEngine _insightsEngine;
    private readonly AdvancedAnalyticsService _advancedAnalytics;
    private readonly ILogger<NaturalLanguageEngine> _logger;

    public NaturalLanguageEngine(
        IEXDataService dataService,
        InsightsEngine insightsEngine,
        AdvancedAnalyticsService advancedAnalytics,
        ILogger<NaturalLanguageEngine> logger)
    {
        _dataService = dataService;
        _insightsEngine = insightsEngine;
        _advancedAnalytics = advancedAnalytics;
        _logger = logger;
    }

    /// <summary>
    /// Process a natural language query and generate an intelligent response
    /// </summary>
    public IntelligentResponse ProcessQuery(string query)
    {
        _logger.LogInformation("Processing natural language query: {Query}", query);

        var response = new IntelligentResponse { Query = query };

        try
        {
            // Parse the query intent
            var intent = ParseQueryIntent(query);

            // Execute based on intent type
            switch (intent.Type)
            {
                case QueryIntentType.GetInsights:
                    return GenerateInsightsResponse(query, intent);

                case QueryIntentType.CompareMarkets:
                    return GenerateMarketComparisonResponse(query, intent);

                case QueryIntentType.Forecast:
                    return GenerateForecastResponse(query, intent);

                case QueryIntentType.BuySellRecommendation:
                    return GenerateRecommendationResponse(query, intent);

                case QueryIntentType.AnalyzePattern:
                    return GeneratePatternAnalysisResponse(query, intent);

                case QueryIntentType.CrossMarketComparison:
                    return GenerateCrossMarketComparisonResponse(query, intent);

                case QueryIntentType.TariffRangeAnalysis:
                    return GenerateTariffRangeResponse(query, intent);

                case QueryIntentType.TimeSlotPeakAnalysis:
                    return GenerateTimeSlotPeakResponse(query, intent);

                case QueryIntentType.CustomChartRequest:
                    return GenerateCustomChartResponse(query, intent);

                case QueryIntentType.MultiYearPerformance:
                    return GenerateMultiYearPerformanceResponse(query, intent);

                case QueryIntentType.StandardDeviation:
                    return GenerateStandardDeviationResponse(query, intent);

                case QueryIntentType.YearWiseComparison:
                    return GenerateYearWiseComparisonResponse(query, intent);

                case QueryIntentType.DataQuery:
                default:
                    return GenerateDataQueryResponse(query, intent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", query);
            response.Answer = "I apologize, but I encountered an error while processing your query. Could you please rephrase it or provide more details?";
            return response;
        }
    }

    /// <summary>
    /// Generate insights-focused response
    /// </summary>
    private IntelligentResponse GenerateInsightsResponse(string query, QueryIntent intent)
    {
        var queryLower = query.ToLower();

        // Check if asking for specific peak/min value - if so, don't include forecasting
        bool askingForSpecificValue = queryLower.Contains("peak") || queryLower.Contains("highest") || queryLower.Contains("maximum") || queryLower.Contains("top") || queryLower.Contains("most") ||
                                       queryLower.Contains("lowest") || queryLower.Contains("minimum") || queryLower.Contains("least") || queryLower.Contains("bottom") || queryLower.Contains("valley");

        // Only forecast if asking about future or if no specific historical date is mentioned
        bool shouldForecast = !askingForSpecificValue && intent.StartDate == null;

        var request = new InsightsRequest
        {
            MarketType = intent.MarketType,
            StartDate = intent.StartDate,
            EndDate = intent.EndDate,
            IncludeForecasting = shouldForecast,
            ForecastDays = 30,
            IncludeRecommendations = !askingForSpecificValue
        };

        var insights = _insightsEngine.GenerateInsights(request);

        var sb = new StringBuilder();
        var marketLabel = string.IsNullOrEmpty(intent.MarketType) ? "all markets" : $"{intent.MarketType} market";

        // Determine if query is about past data (historical)
        bool isPastData = intent.EndDate.HasValue && intent.EndDate.Value < DateTime.Today;

        // Build time period string
        string timePeriod = "";
        if (intent.StartDate.HasValue && intent.EndDate.HasValue)
        {
            var endDateAdjusted = intent.EndDate.Value.AddDays(-1); // EndDate is exclusive, so subtract 1 day

            // Check if it's a single day
            if (intent.StartDate.Value.Date == endDateAdjusted.Date)
            {
                // Single day
                timePeriod = $" on **{intent.StartDate.Value:MMMM dd, yyyy}**";
            }
            else if (intent.StartDate.Value.Year == endDateAdjusted.Year &&
                intent.StartDate.Value.Month == endDateAdjusted.Month)
            {
                // Same month, multiple days
                timePeriod = $" for **{intent.StartDate.Value:MMMM yyyy}**";
            }
            else if (intent.StartDate.Value.Year == endDateAdjusted.Year &&
                     intent.StartDate.Value.Month == 1 && intent.StartDate.Value.Day == 1 &&
                     endDateAdjusted.Month == 12 && endDateAdjusted.Day == 31)
            {
                // Full year (Jan 1 to Dec 31)
                timePeriod = $" for **{intent.StartDate.Value.Year}**";
            }
            else
            {
                timePeriod = $" from **{intent.StartDate.Value:MMM yyyy}** to **{endDateAdjusted:MMM yyyy}**";
            }
        }

        // If asking for peak/min, provide direct answer first
        if (askingForSpecificValue)
        {
            sb.AppendLine($"## 🎯 Direct Answer\n");

            if (queryLower.Contains("peak") || queryLower.Contains("highest") || queryLower.Contains("maximum") || queryLower.Contains("top") || queryLower.Contains("most"))
            {
                if (queryLower.Contains("mcp") || queryLower.Contains("price") || (!queryLower.Contains("mcv") && !queryLower.Contains("volume")))
                {
                    sb.AppendLine($"The **peak MCP (price)**{timePeriod} is **₹{insights.PriceAnalysis.PeakPriceTime.Value:F2}/kWh**");
                    sb.AppendLine($"- Occurred at: **{insights.PriceAnalysis.PeakPriceTime.TimeBlock}**");
                    sb.AppendLine($"- Market: **{marketLabel}**\n");
                }
                if (queryLower.Contains("mcv") || queryLower.Contains("volume"))
                {
                    sb.AppendLine($"The **peak MCV (volume)**{timePeriod} is **{insights.VolumeAnalysis.PeakVolume:F2} GW**");
                    sb.AppendLine($"- Market: **{marketLabel}**\n");
                }
            }
            else if (queryLower.Contains("lowest") || queryLower.Contains("minimum") || queryLower.Contains("least") || queryLower.Contains("bottom") || queryLower.Contains("valley"))
            {
                if (queryLower.Contains("mcp") || queryLower.Contains("price") || (!queryLower.Contains("mcv") && !queryLower.Contains("volume")))
                {
                    sb.AppendLine($"The **lowest MCP (price)**{timePeriod} is **₹{insights.PriceAnalysis.LowestPriceTime.Value:F2}/kWh**");
                    sb.AppendLine($"- Occurred at: **{insights.PriceAnalysis.LowestPriceTime.TimeBlock}**");
                    sb.AppendLine($"- Market: **{marketLabel}**\n");
                }
            }
        }

        // Opening statement
        sb.AppendLine($"## 📊 Analysis Summary\n");
        sb.AppendLine($"Analyzed **{insights.DataPointsAnalyzed:N0}** data points from **{marketLabel}**{timePeriod}\n");

        // Price Analysis
        sb.AppendLine("### 💰 Price Insights\n");
        var priceChange = insights.PriceAnalysis.PercentageChange;
        var priceDirection = priceChange > 0 ? "↗️ increased" : "↘️ decreased";
        var priceVerb = isPastData ? priceDirection : priceDirection;  // Keep same for now, but we'll use it in text
        sb.AppendLine($"• Average price {priceDirection} by **{Math.Abs(priceChange):F2}%**");
        sb.AppendLine($"  - From: ₹{insights.PriceAnalysis.HistoricalAverage:F2}/kWh");
        sb.AppendLine($"  - To: ₹{insights.PriceAnalysis.CurrentAverage:F2}/kWh");
        sb.AppendLine($"• Trend: **{insights.PriceAnalysis.Trend}**");
        sb.AppendLine($"• Volatility: **{insights.PriceAnalysis.Volatility:F2}%** ({GetVolatilityDescription(insights.PriceAnalysis.Volatility)})");
        sb.AppendLine($"• Peak: **₹{insights.PriceAnalysis.PeakPriceTime.Value:F2}/kWh** at {insights.PriceAnalysis.PeakPriceTime.TimeBlock}");
        sb.AppendLine($"• Lowest: **₹{insights.PriceAnalysis.LowestPriceTime.Value:F2}/kWh** at {insights.PriceAnalysis.LowestPriceTime.TimeBlock}\n");

        // Volume Analysis
        sb.AppendLine("### 📦 Volume Insights\n");
        var volumeChange = insights.VolumeAnalysis.PercentageChange;
        var volumeDirection = volumeChange > 0 ? "↗️ increased" : "↘️ decreased";
        sb.AppendLine($"• Trading volume {volumeDirection} by **{Math.Abs(volumeChange):F2}%**");
        sb.AppendLine($"  - From: {insights.VolumeAnalysis.HistoricalAverage:F2} GW");
        sb.AppendLine($"  - To: {insights.VolumeAnalysis.CurrentAverage:F2} GW");
        sb.AppendLine($"• Trend: **{insights.VolumeAnalysis.Trend}**");
        sb.AppendLine($"• Peak Volume: **{insights.VolumeAnalysis.PeakVolume:F2} GW**\n");

        // Key Findings
        var keyFindings = new List<string>();

        // Trends
        if (insights.Trends.Any())
        {
            sb.AppendLine("### 📈 Key Trends\n");
            foreach (var trend in insights.Trends.Take(3))
            {
                sb.AppendLine($"• **{trend.Type}**");
                // Convert to past tense if analyzing historical data
                var description = isPastData ? ConvertToPastTense(trend.Description) : trend.Description;
                sb.AppendLine($"  {description}");
                sb.AppendLine($"  Confidence: {trend.ConfidenceScore:P0}");
                keyFindings.Add(description);
            }
            sb.AppendLine();
        }

        // Patterns
        if (insights.Patterns.Any())
        {
            sb.AppendLine("### 🔍 Identified Patterns\n");
            foreach (var pattern in insights.Patterns)
            {
                sb.AppendLine($"• **{pattern.PatternType}**");
                // Convert to past tense if analyzing historical data
                var description = isPastData ? ConvertToPastTense(pattern.Description) : pattern.Description;
                sb.AppendLine($"  {description}");
                keyFindings.Add(description);
            }
            sb.AppendLine();
        }

        // Anomalies
        if (insights.Anomalies.Any())
        {
            sb.AppendLine($"### ⚠️ Anomalies Detected ({insights.Anomalies.Count})\n");
            foreach (var anomaly in insights.Anomalies.Take(3))
            {
                sb.AppendLine($"• **{anomaly.Type}** - {anomaly.Date:MMM dd, yyyy} at {anomaly.TimeBlock}");
                sb.AppendLine($"  {anomaly.Description}");
            }
            sb.AppendLine();
        }

        // Recommendations
        var recommendations = new List<string>();
        if (insights.Recommendations.Any())
        {
            sb.AppendLine("### 💡 Recommendations\n");
            foreach (var rec in insights.Recommendations)
            {
                var actionIcon = rec.Action switch
                {
                    "Buy" => "🟢",
                    "Sell" => "🔴",
                    "Hold" => "🟡",
                    _ => "ℹ️"
                };
                sb.AppendLine($"**{actionIcon} {rec.Action}** - {rec.MarketType}");
                sb.AppendLine($"• {rec.Reasoning}");
                sb.AppendLine($"• Confidence: **{rec.ConfidenceScore:P0}** | Horizon: **{rec.TimeHorizon}**");
                sb.AppendLine($"\n**Why:**");
                foreach (var factor in rec.SupportingFactors)
                {
                    sb.AppendLine($"  • {factor}");
                }
                sb.AppendLine();
                recommendations.Add($"{rec.Action}: {rec.Reasoning}");
            }
        }

        // Add charts for visualization
        var charts = new List<ChartData>();

        // Add price and volume charts from actual data
        charts.AddRange(CreateInsightsCharts(insights, intent));

        if (insights.Forecast != null && insights.Forecast.Predictions.Any())
        {
            var forecastDays = insights.Forecast.Predictions.Count;
            sb.AppendLine($"## 🔮 {forecastDays}-Day Forecast\n");
            sb.AppendLine($"Based on {insights.Forecast.ModelAccuracy} model:\n");

            var nextWeek = insights.Forecast.Predictions.Take(7).ToList();
            sb.AppendLine("**Next 7 Days Forecast:**");
            foreach (var pred in nextWeek)
            {
                sb.AppendLine($"- {pred.Date:MMM dd}: ₹{pred.PredictedValue:F2}/kWh (Range: ₹{pred.LowerBound:F2} - ₹{pred.UpperBound:F2})");
            }
            sb.AppendLine();

            // Create forecast chart
            charts.Add(CreateForecastChart(insights.Forecast));
        }

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = keyFindings,
            Recommendations = recommendations,
            Charts = charts,
            Data = new Dictionary<string, object>
            {
                ["insights"] = insights
            }
        };
    }

    /// <summary>
    /// Generate market comparison response
    /// </summary>
    private IntelligentResponse GenerateMarketComparisonResponse(string query, QueryIntent intent)
    {
        var allMarkets = new[] { "DAM", "GDAM", "RTM" };
        var marketInsights = new Dictionary<string, MarketInsights>();

        foreach (var market in allMarkets)
        {
            var request = new InsightsRequest
            {
                MarketType = market,
                StartDate = intent.StartDate,
                EndDate = intent.EndDate,
                IncludeForecasting = false,
                IncludeRecommendations = false
            };
            marketInsights[market] = _insightsEngine.GenerateInsights(request);
        }

        var sb = new StringBuilder();
        var queryLower = query.ToLower();

        // Check if asking for specific value
        bool askingForPeak = queryLower.Contains("peak") || queryLower.Contains("maximum") || queryLower.Contains("highest") || queryLower.Contains("max");
        bool askingForLowest = queryLower.Contains("lowest") || queryLower.Contains("minimum") || queryLower.Contains("min");
        bool askingForMCP = queryLower.Contains("mcp") || queryLower.Contains("price");
        bool askingForMCV = queryLower.Contains("mcv") || queryLower.Contains("volume");

        sb.AppendLine("# 📊 Market Analysis\n");

        // Add date range information - show actual data range
        if (intent.StartDate.HasValue && intent.EndDate.HasValue)
        {
            sb.AppendLine($"**📅 Analysis Period**: {intent.StartDate.Value:MMMM dd, yyyy} to {intent.EndDate.Value.AddDays(-1):MMMM dd, yyyy}\n");
        }
        else
        {
            // Get actual date range from the data
            var allData = _dataService.GetAllData();
            if (allData.Any())
            {
                var minDate = allData.Min(d => d.Date);
                var maxDate = allData.Max(d => d.Date);
                var totalRecords = allData.Count();
                sb.AppendLine($"**📅 Analysis Period**: {minDate:MMMM dd, yyyy} to {maxDate:MMMM dd, yyyy} ({totalRecords:N0} records)\n");
            }
            else
            {
                sb.AppendLine($"**📅 Analysis Period**: All available data\n");
            }
        }

        // If specific question about peak/min values, answer it directly first
        if ((askingForPeak || askingForLowest) && marketInsights.Any())
        {
            // Answer for MCP (price)
            if (askingForMCP)
            {
                var peakPriceMarket = marketInsights.OrderByDescending(kvp => kvp.Value.PriceAnalysis.PeakPriceTime.Value).First();
                var lowestPriceMarket = marketInsights.OrderBy(kvp => kvp.Value.PriceAnalysis.LowestPriceTime.Value).First();

                if (askingForPeak)
                {
                    sb.AppendLine($"## 🎯 Direct Answer\n");
                    sb.AppendLine($"The **peak MCP (price) value** across all markets in the specified period is **₹{peakPriceMarket.Value.PriceAnalysis.PeakPriceTime.Value:F2}/kWh** in the **{peakPriceMarket.Key}** market.\n");
                    sb.AppendLine($"This peak occurred at **{peakPriceMarket.Value.PriceAnalysis.PeakPriceTime.TimeBlock}**.\n");
                }
                else if (askingForLowest)
                {
                    sb.AppendLine($"## 🎯 Direct Answer\n");
                    sb.AppendLine($"The **lowest MCP (price) value** across all markets in the specified period is **₹{lowestPriceMarket.Value.PriceAnalysis.LowestPriceTime.Value:F2}/kWh** in the **{lowestPriceMarket.Key}** market.\n");
                    sb.AppendLine($"This lowest value occurred at **{lowestPriceMarket.Value.PriceAnalysis.LowestPriceTime.TimeBlock}**.\n");
                }
            }

            // Answer for MCV (volume)
            if (askingForMCV)
            {
                var peakVolumeMarket = marketInsights.OrderByDescending(kvp => kvp.Value.VolumeAnalysis.PeakVolume).First();
                var lowestVolumeMarket = marketInsights.OrderBy(kvp => kvp.Value.VolumeAnalysis.CurrentAverage).First();

                if (askingForPeak)
                {
                    sb.AppendLine($"## 🎯 Direct Answer\n");
                    sb.AppendLine($"The **peak MCV (volume) value** across all markets in the specified period is **{peakVolumeMarket.Value.VolumeAnalysis.PeakVolume:F2} GW** in the **{peakVolumeMarket.Key}** market.\n");
                }
                else if (askingForLowest)
                {
                    sb.AppendLine($"## 🎯 Direct Answer\n");
                    sb.AppendLine($"The **lowest MCV (volume) value** across all markets in the specified period is **{lowestVolumeMarket.Value.VolumeAnalysis.CurrentAverage:F2} GW** in the **{lowestVolumeMarket.Key}** market.\n");
                }
            }
        }

        sb.AppendLine("### 📋 Detailed Comparison\n");

        // Price Comparison
        sb.AppendLine("**💰 Price Comparison**\n");
        sb.AppendLine("| Market | Avg Price | Trend | Volatility | Change |");
        sb.AppendLine("|--------|-----------|-------|------------|--------|");
        foreach (var kvp in marketInsights)
        {
            var p = kvp.Value.PriceAnalysis;
            sb.AppendLine($"| **{kvp.Key}** | ₹{p.CurrentAverage:F2}/kWh | {p.Trend} | {p.Volatility:F1}% | {(p.PercentageChange > 0 ? "+" : "")}{p.PercentageChange:F2}% |");
        }
        sb.AppendLine();

        // Volume Comparison
        sb.AppendLine("\n**📦 Volume Comparison**\n");
        sb.AppendLine("| Market | Avg Volume | Trend | Peak Volume | Change |");
        sb.AppendLine("|--------|------------|-------|-------------|--------|");
        foreach (var kvp in marketInsights)
        {
            var v = kvp.Value.VolumeAnalysis;
            sb.AppendLine($"| **{kvp.Key}** | {v.CurrentAverage:F2} GW | {v.Trend} | {v.PeakVolume:F2} GW | {(v.PercentageChange > 0 ? "+" : "")}{v.PercentageChange:F2}% |");
        }
        sb.AppendLine();

        // Best market recommendations
        var bestForBuying = marketInsights.OrderBy(kvp => kvp.Value.PriceAnalysis.CurrentAverage).First();
        var bestForSelling = marketInsights.OrderByDescending(kvp => kvp.Value.PriceAnalysis.CurrentAverage).First();
        var mostStable = marketInsights.OrderBy(kvp => kvp.Value.PriceAnalysis.Volatility).First();

        sb.AppendLine("### 💡 Quick Recommendations\n");
        sb.AppendLine($"**🟢 Best for Buying: {bestForBuying.Key}**");
        sb.AppendLine($"  • Lowest price: ₹{bestForBuying.Value.PriceAnalysis.CurrentAverage:F2}/kWh");
        sb.AppendLine($"  • Ideal for purchasing energy at competitive rates\n");

        sb.AppendLine($"**🔴 Best for Selling: {bestForSelling.Key}**");
        sb.AppendLine($"  • Highest price: ₹{bestForSelling.Value.PriceAnalysis.CurrentAverage:F2}/kWh");
        sb.AppendLine($"  • Maximize revenue by selling here\n");

        sb.AppendLine($"**🟡 Most Stable: {mostStable.Key}**");
        sb.AppendLine($"  • Volatility: {mostStable.Value.PriceAnalysis.Volatility:F2}%");
        sb.AppendLine($"  • Best for risk-averse trading\n");

        var keyFindings = new List<string>
        {
            $"{bestForBuying.Key} offers the lowest prices for buying",
            $"{bestForSelling.Key} provides the highest prices for selling",
            $"{mostStable.Key} is the most stable market"
        };

        var recommendations = new List<string>
        {
            $"Consider buying from {bestForBuying.Key} to minimize costs",
            $"Target {bestForSelling.Key} for selling to maximize revenue",
            $"Use {mostStable.Key} for predictable, lower-risk transactions"
        };

        // Create chart titles with date range
        var dateRangeSuffix = intent.StartDate.HasValue && intent.EndDate.HasValue
            ? $" ({intent.StartDate.Value:MMM dd, yyyy} - {intent.EndDate.Value.AddDays(-1):MMM dd, yyyy})"
            : " (All Data)";

        // Create comparison chart
        var charts = new List<ChartData>
        {
            new ChartData
            {
                ChartType = "Bar",
                Title = $"Average Price Comparison Across Markets{dateRangeSuffix}",
                Labels = allMarkets.ToList(),
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Average Price (₹/kWh)",
                        Data = allMarkets.Select(m => marketInsights[m].PriceAnalysis.CurrentAverage).ToList(),
                        Color = "rgba(15, 76, 129, 0.8)",
                        BorderColor = "rgba(15, 76, 129, 1)"
                    }
                }
            },
            new ChartData
            {
                ChartType = "Bar",
                Title = $"Average Volume Comparison Across Markets{dateRangeSuffix}",
                Labels = allMarkets.ToList(),
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Average Volume (GW)",
                        Data = allMarkets.Select(m => marketInsights[m].VolumeAnalysis.CurrentAverage).ToList(),
                        Color = "rgba(20, 184, 166, 0.8)",
                        BorderColor = "rgba(20, 184, 166, 1)"
                    }
                }
            }
        };

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = keyFindings,
            Recommendations = recommendations,
            Charts = charts,
            Data = new Dictionary<string, object>
            {
                ["marketComparison"] = marketInsights
            }
        };
    }

    /// <summary>
    /// Generate forecast-focused response
    /// </summary>
    private IntelligentResponse GenerateForecastResponse(string query, QueryIntent intent)
    {
        // Check if this is a time slot specific forecast query
        var timeSlotInfo = ExtractTimeSlotRange(query);
        if (timeSlotInfo.HasTimeSlots)
        {
            return GenerateTimeSlotForecastResponse(query, intent, timeSlotInfo);
        }

        var forecastDays = ExtractForecastDays(query);
        var request = new InsightsRequest
        {
            MarketType = intent.MarketType,
            StartDate = intent.StartDate,
            EndDate = intent.EndDate,
            IncludeForecasting = true,
            ForecastDays = forecastDays,
            IncludeRecommendations = true
        };

        var insights = _insightsEngine.GenerateInsights(request);

        var sb = new StringBuilder();
        var marketLabel = string.IsNullOrEmpty(intent.MarketType) ? "all markets" : $"{intent.MarketType} market";

        sb.AppendLine($"# 🔮 {forecastDays}-Day Price Forecast for {marketLabel}\n");

        if (insights.Forecast != null && insights.Forecast.Predictions.Any())
        {
            sb.AppendLine($"Using advanced **{insights.Forecast.ModelAccuracy}** with **{insights.Forecast.ConfidenceScore:P0}** confidence:\n");

            // Weekly forecast summary
            var weeks = insights.Forecast.Predictions
                .GroupBy(p => (p.Date.Date - insights.Forecast.ForecastStartDate).Days / 7)
                .Take(4)
                .ToList();

            foreach (var week in weeks)
            {
                var weekNum = week.Key + 1;
                var avgPrice = week.Average(p => p.PredictedValue);
                var minPrice = week.Min(p => p.LowerBound);
                var maxPrice = week.Max(p => p.UpperBound);

                sb.AppendLine($"### Week {weekNum} ({week.First().Date:MMM dd} - {week.Last().Date:MMM dd})");
                sb.AppendLine($"- **Average Predicted Price**: ₹{avgPrice:F2}/kWh");
                sb.AppendLine($"- **Expected Range**: ₹{minPrice:F2} - ₹{maxPrice:F2}/kWh");
                sb.AppendLine();
            }

            // Trend analysis
            var firstWeekAvg = weeks.First().Average(p => p.PredictedValue);
            var lastWeekAvg = weeks.Last().Average(p => p.PredictedValue);
            var trend = lastWeekAvg > firstWeekAvg ? "increasing" : lastWeekAvg < firstWeekAvg ? "decreasing" : "stable";
            var trendPercent = firstWeekAvg > 0 ? ((lastWeekAvg - firstWeekAvg) / firstWeekAvg) * 100 : 0;

            sb.AppendLine($"## 📊 Forecast Trend\n");
            sb.AppendLine($"Prices are expected to be **{trend}** over the next {forecastDays} days, with a {(trendPercent > 0 ? "+" : "")}{trendPercent:F2}% change.\n");

            // Business implications
            sb.AppendLine($"## 💡 Business Implications\n");
            if (trend == "increasing")
            {
                sb.AppendLine("- **Buyers**: Consider purchasing now before prices rise");
                sb.AppendLine("- **Sellers**: Wait for higher prices in the coming weeks");
                sb.AppendLine("- **Strategy**: Lock in current rates for purchasing, delay selling\n");
            }
            else if (trend == "decreasing")
            {
                sb.AppendLine("- **Buyers**: Wait for lower prices in the coming weeks");
                sb.AppendLine("- **Sellers**: Sell now before prices drop");
                sb.AppendLine("- **Strategy**: Delay purchasing, sell current inventory\n");
            }
            else
            {
                sb.AppendLine("- **Market Outlook**: Stable prices expected");
                sb.AppendLine("- **Strategy**: Normal trading operations, no urgency\n");
            }
        }
        else
        {
            sb.AppendLine("Insufficient historical data to generate a reliable forecast. Please ensure at least 30 days of data is available.");
        }

        var charts = insights.Forecast != null ? new List<ChartData> { CreateForecastChart(insights.Forecast) } : new List<ChartData>();

        var keyFindings = new List<string>();
        if (insights.Forecast != null && insights.Forecast.Predictions.Any())
        {
            var weeks = insights.Forecast.Predictions
                .GroupBy(p => (p.Date.Date - insights.Forecast.ForecastStartDate).Days / 7)
                .Take(4)
                .ToList();

            if (weeks.Any())
            {
                var firstWeekAvg = weeks.First().Average(p => p.PredictedValue);
                var lastWeekAvg = weeks.Last().Average(p => p.PredictedValue);
                var trend = lastWeekAvg > firstWeekAvg ? "increasing" : lastWeekAvg < firstWeekAvg ? "decreasing" : "stable";
                var trendPercent = firstWeekAvg > 0 ? ((lastWeekAvg - firstWeekAvg) / firstWeekAvg) * 100 : 0;

                keyFindings.Add($"Prices expected to {trend} by {Math.Abs(trendPercent):F2}%");
            }
        }

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = keyFindings,
            Recommendations = insights.Recommendations.Select(r => $"{r.Action}: {r.Reasoning}").ToList(),
            Charts = charts,
            Data = new Dictionary<string, object> { ["forecast"] = insights.Forecast ?? new object() }
        };
    }

    /// <summary>
    /// Generate buy/sell recommendation response
    /// </summary>
    private IntelligentResponse GenerateRecommendationResponse(string query, QueryIntent intent)
    {
        var request = new InsightsRequest
        {
            MarketType = intent.MarketType,
            IncludeForecasting = true,
            ForecastDays = 30,
            IncludeRecommendations = true
        };

        var insights = _insightsEngine.GenerateInsights(request);

        var sb = new StringBuilder();
        sb.AppendLine("# 💼 Trading Recommendations\n");

        if (insights.Recommendations.Any())
        {
            foreach (var rec in insights.Recommendations)
            {
                var icon = rec.Action switch
                {
                    "Buy" => "🟢",
                    "Sell" => "🔴",
                    "Hold" => "🟡",
                    "Caution" => "⚠️",
                    _ => "ℹ️"
                };

                sb.AppendLine($"## {icon} {rec.Action} Recommendation - {rec.MarketType}\n");
                sb.AppendLine($"**Confidence**: {rec.ConfidenceScore:P0} | **Time Horizon**: {rec.TimeHorizon}\n");
                sb.AppendLine($"### Analysis\n");
                sb.AppendLine($"{rec.Reasoning}\n");
                sb.AppendLine($"### Supporting Evidence\n");
                foreach (var factor in rec.SupportingFactors)
                {
                    sb.AppendLine($"- {factor}");
                }
                sb.AppendLine();

                if (rec.ExpectedPriceRange.HasValue)
                {
                    sb.AppendLine($"**Expected Price**: ₹{rec.ExpectedPriceRange:F2}/kWh\n");
                }
            }
        }
        else
        {
            sb.AppendLine("No specific recommendations at this time. Market conditions are neutral.");
        }

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            Recommendations = insights.Recommendations.Select(r => $"{r.Action}: {r.Reasoning}").ToList(),
            KeyFindings = insights.Recommendations.SelectMany(r => r.SupportingFactors).ToList(),
            Charts = new List<ChartData>(),
            Data = new Dictionary<string, object> { ["recommendations"] = insights.Recommendations }
        };
    }

    /// <summary>
    /// Generate pattern analysis response
    /// </summary>
    private IntelligentResponse GeneratePatternAnalysisResponse(string query, QueryIntent intent)
    {
        var queryLower = query.ToLower();
        var sb = new StringBuilder();

        // Check if query is specifically asking for anomalies
        bool isAnomalyQuery = queryLower.Contains("anomal") || queryLower.Contains("unusual") || queryLower.Contains("spike") || queryLower.Contains("outlier");

        // If no market specified and asking about anomalies, analyze all markets
        if (string.IsNullOrEmpty(intent.MarketType) && isAnomalyQuery)
        {
            // Default to September 2025 (most recent month with data) if no date specified
            // Fallback to current month only if it's before October 2025
            DateTime defaultStartDate;
            DateTime defaultEndDate;

            if (intent.StartDate == null && intent.EndDate == null)
            {
                // Default to September 2025 which should have data
                defaultStartDate = new DateTime(2025, 9, 1);
                defaultEndDate = new DateTime(2025, 10, 1);
            }
            else
            {
                defaultStartDate = intent.StartDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                defaultEndDate = intent.EndDate ?? DateTime.Today.AddDays(1);
            }

            var startDate = defaultStartDate;
            var endDate = defaultEndDate;

            sb.AppendLine($"# ⚠️ Anomaly Analysis\n");
            sb.AppendLine($"**Period**: {startDate:MMMM dd, yyyy} to {endDate.AddDays(-1):MMMM dd, yyyy}\n");

            var allAnomalies = new List<(string Market, List<AnomalyInsight> Anomalies)>();

            // Analyze each market
            foreach (var market in new[] { "DAM", "GDAM", "RTM" })
            {
                var request = new InsightsRequest
                {
                    MarketType = market,
                    StartDate = startDate,
                    EndDate = endDate
                };

                var insights = _insightsEngine.GenerateInsights(request);
                if (insights.Anomalies.Any())
                {
                    allAnomalies.Add((market, insights.Anomalies));
                }
            }

            // Display anomalies grouped by market
            if (allAnomalies.Any())
            {
                int totalAnomalies = allAnomalies.Sum(a => a.Anomalies.Count);
                sb.AppendLine($"**Total Anomalies Found**: {totalAnomalies} across {allAnomalies.Count} market(s)\n");

                foreach (var (market, anomalies) in allAnomalies)
                {
                    sb.AppendLine($"## 📊 {market} Market ({anomalies.Count} anomalies)\n");

                    foreach (var anomaly in anomalies.Take(5))
                    {
                        sb.AppendLine($"- **{anomaly.Date:MMM dd, yyyy} at {anomaly.TimeBlock}** - {anomaly.Type}");
                        sb.AppendLine($"  - {anomaly.Description}");
                        sb.AppendLine($"  - Severity: **{anomaly.Severity}** | Deviation: {anomaly.Deviation:F2}%\n");
                    }

                    if (anomalies.Count > 5)
                    {
                        sb.AppendLine($"*...and {anomalies.Count - 5} more anomalies*\n");
                    }
                }
            }
            else
            {
                sb.AppendLine("✅ No significant anomalies detected across all markets for this period.\n");
            }

            return new IntelligentResponse
            {
                Query = query,
                Answer = sb.ToString(),
                KeyFindings = allAnomalies.SelectMany(a => a.Anomalies.Take(3).Select(an => $"{a.Market}: {an.Type} on {an.Date:MMM dd}")).ToList(),
                Charts = new List<ChartData>(),
                Data = new Dictionary<string, object>
                {
                    ["anomalies"] = allAnomalies.SelectMany(a => a.Anomalies).ToList()
                }
            };
        }

        // Single market analysis
        var singleRequest = new InsightsRequest
        {
            MarketType = intent.MarketType,
            StartDate = intent.StartDate,
            EndDate = intent.EndDate
        };

        var singleInsights = _insightsEngine.GenerateInsights(singleRequest);

        sb.AppendLine("# 🔍 Pattern Analysis\n");

        if (singleInsights.Patterns.Any())
        {
            foreach (var pattern in singleInsights.Patterns)
            {
                sb.AppendLine($"## {pattern.PatternType} Pattern (Confidence: {pattern.Confidence:P0})\n");
                sb.AppendLine($"{pattern.Description}\n");
                if (pattern.TimeWindows.Any())
                {
                    sb.AppendLine($"**Key Time Windows**: {string.Join(", ", pattern.TimeWindows)}\n");
                }
            }
        }

        if (singleInsights.Anomalies.Any())
        {
            sb.AppendLine($"## ⚠️ Anomalies Detected ({singleInsights.Anomalies.Count})\n");
            foreach (var anomaly in singleInsights.Anomalies.Take(5))
            {
                sb.AppendLine($"- **{anomaly.Severity} Severity**: {anomaly.Description}");
            }
        }

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = singleInsights.Patterns.Select(p => p.Description).ToList(),
            Charts = new List<ChartData>(),
            Data = new Dictionary<string, object>
            {
                ["patterns"] = singleInsights.Patterns,
                ["anomalies"] = singleInsights.Anomalies
            }
        };
    }

    /// <summary>
    /// Generate data query response
    /// </summary>
    private IntelligentResponse GenerateDataQueryResponse(string query, QueryIntent intent)
    {
        // This handles basic data queries - can be enhanced further
        return new IntelligentResponse
        {
            Query = query,
            Answer = "I understand you're looking for specific data. Let me help you with that.",
            KeyFindings = new List<string>(),
            Recommendations = new List<string>(),
            Charts = new List<ChartData>()
        };
    }

    /// <summary>
    /// Generate cross-market comparison response
    /// </summary>
    private IntelligentResponse GenerateCrossMarketComparisonResponse(string query, QueryIntent intent)
    {
        var sb = new StringBuilder();
        var startDate = intent.StartDate ?? new DateTime(2025, 9, 1);
        var endDate = intent.EndDate ?? new DateTime(2025, 10, 1);

        // GetDataByDateRange uses inclusive endDate, so subtract 1 day to exclude the next month
        var inclusiveEndDate = endDate.AddDays(-1);
        var comparisons = _insightsEngine.AnalyzeCrossMarketComparisons(startDate, inclusiveEndDate);

        sb.AppendLine($"# 📊 Cross-Market Comparison Analysis\n");
        sb.AppendLine($"**Period**: {startDate:MMMM dd, yyyy} to {inclusiveEndDate:MMMM dd, yyyy}\n");

        foreach (var comp in comparisons)
        {
            sb.AppendLine($"## {comp.ComparisonType}\n");
            sb.AppendLine($"- **Total Slots**: {comp.TotalSlots:N0}");
            sb.AppendLine($"- **Percentage**: {comp.Percentage:F2}%\n");

            if (comp.Slots.Any())
            {
                sb.AppendLine($"**Sample Time Slots** (showing first 5):\n");
                foreach (var slot in comp.Slots.Take(5))
                {
                    sb.AppendLine($"- {slot.Date:MMM dd, yyyy} at {slot.TimeBlock}: Market1=₹{slot.Market1Value:F2}, Market2=₹{slot.Market2Value:F2}, Diff=₹{slot.Difference:F2}");
                }
                sb.AppendLine();
            }
        }

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = comparisons.Select(c => $"{c.ComparisonType}: {c.TotalSlots:N0} slots ({c.Percentage:F2}%)").ToList(),
            Charts = new List<ChartData>(),
            Data = new Dictionary<string, object>
            {
                ["comparisons"] = comparisons
            }
        };
    }

    /// <summary>
    /// Generate tariff range analysis response
    /// </summary>
    private IntelligentResponse GenerateTariffRangeResponse(string query, QueryIntent intent)
    {
        var sb = new StringBuilder();

        // Detect if query is for MCV or MCP
        var metric = query.ToLower().Contains("mcv") || query.ToLower().Contains("volume") ? "MCV" : "MCP";

        // Check for specific price range in query (e.g., "9-10", "between 9 and 10")
        decimal? specificMinRange = null;
        decimal? specificMaxRange = null;
        var queryLower = query.ToLower();

        // Pattern 1: "9-10Rs", "9-10 Rs", "9 to 10", "9-10"
        var rangeMatch1 = Regex.Match(queryLower, @"(\d+\.?\d*)\s*-\s*(\d+\.?\d*)");
        if (rangeMatch1.Success)
        {
            specificMinRange = decimal.Parse(rangeMatch1.Groups[1].Value);
            specificMaxRange = decimal.Parse(rangeMatch1.Groups[2].Value);
        }

        // Pattern 2: "between X and Y", "from X to Y"
        var rangeMatch2 = Regex.Match(queryLower, @"(?:between|from)\s+(\d+\.?\d*)\s+(?:and|to)\s+(\d+\.?\d*)");
        if (rangeMatch2.Success)
        {
            specificMinRange = decimal.Parse(rangeMatch2.Groups[1].Value);
            specificMaxRange = decimal.Parse(rangeMatch2.Groups[2].Value);
        }

        var results = _insightsEngine.AnalyzeTariffRanges(metric, intent.StartDate, intent.EndDate);

        var period = intent.StartDate.HasValue && intent.EndDate.HasValue
            ? $"**Period**: {intent.StartDate.Value:MMMM dd, yyyy} to {intent.EndDate.Value.AddDays(-1):MMMM dd, yyyy}\n\n"
            : "**Period**: All available data\n\n";

        var analysisTitle = metric == "MCV" ? "Volume Range Analysis" : "Tariff Range Analysis";
        var icon = metric == "MCV" ? "📊" : "💰";

        // If specific range detected, provide direct answer first
        if (specificMinRange.HasValue && specificMaxRange.HasValue)
        {
            // Get data for specific range calculation
            var allData = intent.StartDate.HasValue && intent.EndDate.HasValue
                ? _dataService.GetDataByDateRange(intent.StartDate.Value, intent.EndDate.Value).ToList()
                : _dataService.GetAllData().ToList();

            var minVal = specificMinRange.Value;
            var maxVal = specificMaxRange.Value;
            var unit = metric == "MCV" ? "GW" : "Rs./kWh";

            sb.AppendLine($"# {icon} Specific Range Query: {minVal}-{maxVal} {unit}\n");
            sb.AppendLine(period);

            // Calculate counts for each market
            var marketCounts = new Dictionary<string, int>();
            var totalCount = 0;

            foreach (var market in new[] { "DAM", "GDAM", "RTM" })
            {
                var marketData = allData.Where(d => d.Type == market).ToList();
                int count;

                if (metric == "MCV")
                {
                    count = marketData.Count(d => d.MCV >= minVal && d.MCV <= maxVal);
                }
                else
                {
                    count = marketData.Count(d => d.MCP >= minVal && d.MCP <= maxVal);
                }

                marketCounts[market] = count;
                totalCount += count;
            }

            // Direct answer
            sb.AppendLine("## 🎯 Direct Answer\n");
            sb.AppendLine($"**Total time blocks within {minVal}-{maxVal} {unit}**: **{totalCount:N0}** slots\n");
            sb.AppendLine("### Breakdown by Market:\n");
            foreach (var kvp in marketCounts)
            {
                sb.AppendLine($"- **{kvp.Key}**: {kvp.Value:N0} slots");
            }
            sb.AppendLine();
            sb.AppendLine("---\n");
        }

        sb.AppendLine($"# {icon} {analysisTitle}\n");
        sb.AppendLine(period);

        // Group by market
        var marketGroups = results.GroupBy(r => r.MarketType);
        foreach (var marketGroup in marketGroups)
        {
            sb.AppendLine($"## 📈 {marketGroup.Key} Market\n");

            // Create markdown table
            sb.AppendLine("| Range | Slots | Percentage |");
            sb.AppendLine("|:------|------:|-----------:|");

            foreach (var bucket in marketGroup.OrderBy(b => b.MinValue))
            {
                var percentBar = CreatePercentageBar(bucket.Percentage);
                sb.AppendLine($"| **{bucket.RangeDescription}** | {bucket.SlotCount:N0} | {bucket.Percentage:F2}% {percentBar} |");
            }

            sb.AppendLine();
        }

        // Add key insights section
        var keyInsights = results.Where(r => r.Percentage > 15).OrderByDescending(r => r.Percentage).ToList();
        if (keyInsights.Any())
        {
            sb.AppendLine("## 🔑 Key Insights\n");
            foreach (var insight in keyInsights)
            {
                sb.AppendLine($"- **{insight.MarketType}**: {insight.RangeDescription} dominates with **{insight.Percentage:F1}%** ({insight.SlotCount:N0} slots)");
            }
            sb.AppendLine();
        }

        // Create interactive charts
        var charts = CreateTariffRangeCharts(results, metric);

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = results.Where(r => r.Percentage > 10).Select(r => $"{r.MarketType} - {r.RangeDescription}: {r.SlotCount:N0} slots ({r.Percentage:F2}%)").ToList(),
            Charts = charts,
            Data = new Dictionary<string, object>
            {
                ["tariff_ranges"] = results
            }
        };
    }

    /// <summary>
    /// Create a simple text-based percentage bar
    /// </summary>
    private string CreatePercentageBar(decimal percentage)
    {
        var barLength = (int)Math.Round(percentage / 5); // Each block = 5%
        return barLength > 0 ? new string('▓', Math.Min(barLength, 20)) : "";
    }

    /// <summary>
    /// Generate time slot peak analysis response
    /// </summary>
    private IntelligentResponse GenerateTimeSlotPeakResponse(string query, QueryIntent intent)
    {
        var sb = new StringBuilder();

        // Detect if query is for MCV or MCP
        var metric = query.ToLower().Contains("mcv") || query.ToLower().Contains("volume") ? "MCV" : "MCP";
        var results = _insightsEngine.AnalyzeTimeSlotsByMarket(metric, intent.StartDate, intent.EndDate);

        var period = intent.StartDate.HasValue && intent.EndDate.HasValue
            ? $"**Period**: {intent.StartDate.Value:MMMM dd, yyyy} to {intent.EndDate.Value.AddDays(-1):MMMM dd, yyyy}\n"
            : "**Period**: All available data\n";

        var analysisTitle = metric == "MCV" ? "Time Slot Volume Analysis" : "Time Slot Peak Analysis";
        var metricLabel = metric == "MCV" ? "MCV" : "MCP";
        var unit = metric == "MCV" ? "GW" : "₹/kWh";

        sb.AppendLine($"# ⏰ {analysisTitle}\n");
        sb.AppendLine(period);

        foreach (var analysis in results)
        {
            sb.AppendLine($"## {analysis.MarketType} Market\n");
            sb.AppendLine($"**Summary**: {analysis.Summary}\n");

            sb.AppendLine($"### Peak Hours (Top 25% highest {metricLabel}):");
            sb.AppendLine($"{string.Join(", ", analysis.PeakHours.Take(10))}\n");

            sb.AppendLine($"### Off-Peak Hours (Bottom 25% lowest {metricLabel}):");
            sb.AppendLine($"{string.Join(", ", analysis.OffPeakHours.Take(10))}\n");

            // Show top 5 time slots with highest metric value
            var topSlots = analysis.TimeSlots.OrderByDescending(t => t.AverageMCP).Take(5);
            sb.AppendLine($"### Top 5 Time Slots by Average {metricLabel}:");
            foreach (var slot in topSlots)
            {
                if (metric == "MCV")
                {
                    sb.AppendLine($"- **{slot.TimeBlock}**: Avg={slot.AverageMCP:F2} GW (Max={slot.MaxMCP:F2}, Min={slot.MinMCP:F2})");
                }
                else
                {
                    sb.AppendLine($"- **{slot.TimeBlock}**: Avg=₹{slot.AverageMCP:F2}/kWh (Max=₹{slot.MaxMCP:F2}, Min=₹{slot.MinMCP:F2})");
                }
            }
            sb.AppendLine();
        }

        // Create interactive charts
        var charts = CreateTimeSlotPeakCharts(results, metric);

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = results.Select(r => r.Summary).ToList(),
            Charts = charts,
            Data = new Dictionary<string, object>
            {
                ["time_slot_analysis"] = results
            }
        };
    }

    /// <summary>
    /// Generate custom chart response based on user-specified chart types and metrics
    /// </summary>
    private IntelligentResponse GenerateCustomChartResponse(string query, QueryIntent intent)
    {
        var sb = new StringBuilder();
        var charts = new List<ChartData>();

        // Default to all metrics if none specified
        if (!intent.RequestedMetrics.Any())
        {
            intent.RequestedMetrics.Add("MCP");
            intent.RequestedMetrics.Add("MCV");
        }

        // Determine markets to query
        var markets = new List<string>();
        if (string.IsNullOrEmpty(intent.MarketType) || query.ToLower().Contains("all"))
        {
            markets = new List<string> { "DAM", "GDAM", "RTM" };
        }
        else
        {
            markets.Add(intent.MarketType);
        }

        var period = intent.StartDate.HasValue && intent.EndDate.HasValue
            ? $"**Period**: {intent.StartDate.Value:MMMM dd, yyyy} to {intent.EndDate.Value.AddDays(-1):MMMM dd, yyyy}\n\n"
            : "**Period**: All available data\n\n";

        sb.AppendLine($"# 📊 Custom Market Analysis\n");
        sb.AppendLine(period);

        if (!string.IsNullOrEmpty(intent.TimeSlotStart) && !string.IsNullOrEmpty(intent.TimeSlotEnd))
        {
            sb.AppendLine($"**Time Slot**: {intent.TimeSlotStart} to {intent.TimeSlotEnd}\n");
        }

        sb.AppendLine($"**Markets**: {string.Join(", ", markets)}\n");
        sb.AppendLine($"**Metrics**: {string.Join(", ", intent.RequestedMetrics)}\n");

        // Get data for each market and metric
        var marketData = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var market in markets)
        {
            marketData[market] = new Dictionary<string, decimal>();

            // Get all data and filter by market and date range
            var allData = _dataService.GetAllData()
                .Where(d => d.Type == market);

            if (intent.StartDate.HasValue)
                allData = allData.Where(d => d.Date >= intent.StartDate.Value);

            if (intent.EndDate.HasValue)
                allData = allData.Where(d => d.Date < intent.EndDate.Value);

            // Filter by time slot if specified
            if (!string.IsNullOrEmpty(intent.TimeSlotStart) && !string.IsNullOrEmpty(intent.TimeSlotEnd))
            {
                allData = allData.Where(d => IsInTimeSlot(d.TimeBlock, intent.TimeSlotStart, intent.TimeSlotEnd));
            }

            var filteredData = allData.ToList();

            if (intent.RequestedMetrics.Contains("MCP"))
            {
                marketData[market]["MCP"] = filteredData.Any() ? filteredData.Average(d => d.MCP) : 0;
            }

            if (intent.RequestedMetrics.Contains("MCV"))
            {
                marketData[market]["MCV"] = filteredData.Any() ? filteredData.Average(d => d.MCV) : 0;
            }
        }

        // Create summary table
        sb.AppendLine("## 📈 Market Summary\n");
        sb.AppendLine("| Market | " + string.Join(" | ", intent.RequestedMetrics.Select(m => m == "MCP" ? "Avg Price (₹/kWh)" : "Avg Volume (GW)")) + " |");
        sb.AppendLine("|:-------|" + string.Join("|", intent.RequestedMetrics.Select(_ => "---------------:")) + "|");

        foreach (var market in markets)
        {
            var values = intent.RequestedMetrics.Select(m => marketData[market][m].ToString("F2"));
            sb.AppendLine($"| **{market}** | " + string.Join(" | ", values) + " |");
        }
        sb.AppendLine();

        // Create charts based on user specifications
        foreach (var metric in intent.RequestedMetrics)
        {
            var chartType = intent.ChartTypes.ContainsKey(metric) ? intent.ChartTypes[metric] : "Bar";
            var metricLabel = metric == "MCP" ? "Price (₹/kWh)" : "Volume (GW)";
            var unit = metric == "MCP" ? "₹/kWh" : "GW";

            var chartData = new ChartData
            {
                ChartType = chartType,
                Title = $"{metric} Across Markets - {metricLabel}",
                Labels = markets,
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = $"Average {metric}",
                        Data = markets.Select(m => marketData[m][metric]).ToList(),
                        Color = metric == "MCP" ? "rgba(15, 76, 129, 0.8)" : "rgba(20, 184, 166, 0.8)",
                        BorderColor = metric == "MCP" ? "rgba(15, 76, 129, 1)" : "rgba(20, 184, 166, 1)",
                        Fill = chartType == "Line" ? false : true,
                        Tension = chartType == "Line" ? 0.4m : (decimal?)null
                    }
                }
            };

            charts.Add(chartData);
        }

        // Add key findings
        var keyFindings = new List<string>();
        foreach (var metric in intent.RequestedMetrics)
        {
            var maxMarket = markets.OrderByDescending(m => marketData[m][metric]).First();
            var minMarket = markets.OrderBy(m => marketData[m][metric]).First();

            keyFindings.Add($"{metric}: {maxMarket} has highest average ({marketData[maxMarket][metric]:F2}), {minMarket} has lowest ({marketData[minMarket][metric]:F2})");
        }

        sb.AppendLine("## 🔑 Key Insights\n");
        foreach (var finding in keyFindings)
        {
            sb.AppendLine($"- {finding}");
        }

        return new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = keyFindings,
            Charts = charts,
            Data = new Dictionary<string, object>
            {
                ["market_data"] = marketData
            }
        };
    }

    /// <summary>
    /// Check if a time block falls within the specified time slot
    /// </summary>
    private bool IsInTimeSlot(string timeBlock, string startHour, string endHour)
    {
        // Parse time block (format: "1", "2", "3", etc. where 1 = 00:00-00:15, 2 = 00:15-00:30, etc.)
        if (!int.TryParse(timeBlock, out int blockNum))
            return false;

        // Convert block number to hour (each hour has 4 blocks: 1-4 = hour 0, 5-8 = hour 1, etc.)
        int hour = (blockNum - 1) / 4;

        // Parse start and end hours
        if (!int.TryParse(startHour, out int start))
            return false;
        if (!int.TryParse(endHour, out int end))
            return false;

        // Check if hour is within range
        return hour >= start && hour < end;
    }

    // Helper Methods

    private QueryIntent ParseQueryIntent(string query)
    {
        var queryLower = query.ToLower();
        var intent = new QueryIntent { OriginalQuery = query };

        // Determine intent type
        // Check for custom chart requests FIRST (highest priority)
        if (Regex.IsMatch(queryLower, @"\b(generate|create|show|plot|draw|make)\s+.*\s*(bar\s+chart|line\s+graph|line\s+chart|pie\s+chart|chart|graph)") ||
            Regex.IsMatch(queryLower, @"\b(bar\s+chart|line\s+graph|line\s+chart|pie\s+chart)\s+(for|of|with)"))
        {
            intent.Type = QueryIntentType.CustomChartRequest;
            _logger.LogInformation("Query intent matched: CustomChartRequest");

            // Extract requested metrics (MCV, MCP)
            if (Regex.IsMatch(queryLower, @"\bmcv\b"))
                intent.RequestedMetrics.Add("MCV");
            if (Regex.IsMatch(queryLower, @"\bmcp\b"))
                intent.RequestedMetrics.Add("MCP");

            // Extract chart types for each metric
            var barMatch = Regex.Match(queryLower, @"(bar\s+chart)\s+(for|of)\s+(mcv|mcp)");
            if (barMatch.Success)
            {
                var metric = barMatch.Groups[3].Value.ToUpper();
                intent.ChartTypes[metric] = "Bar";
            }

            var lineMatch = Regex.Match(queryLower, @"(line\s+(graph|chart))\s+(for|of)\s+(mcv|mcp)");
            if (lineMatch.Success)
            {
                var metric = lineMatch.Groups[4].Value.ToUpper();
                intent.ChartTypes[metric] = "Line";
            }

            // Extract time slot if specified (e.g., "9AM to 5PM", "10:00 to 18:00")
            var timeSlotMatch = Regex.Match(query, @"(\d{1,2})\s*(?:AM|PM|:00)?\s+to\s+(\d{1,2})\s*(?:AM|PM|:00)?", RegexOptions.IgnoreCase);
            if (timeSlotMatch.Success)
            {
                intent.TimeSlotStart = timeSlotMatch.Groups[1].Value;
                intent.TimeSlotEnd = timeSlotMatch.Groups[2].Value;
                _logger.LogInformation("Extracted time slot: {Start} to {End}", intent.TimeSlotStart, intent.TimeSlotEnd);
            }
        }
        // Check for multi-year performance queries (e.g., "from 2023 till 2025", "2023-2025", "2023 to 2025")
        else if (Regex.IsMatch(queryLower, @"\b(\d{4})\s*(till|to|through|-|until)\s*(\d{4})\b") ||
                 Regex.IsMatch(queryLower, @"\bfrom\s+(\d{4})\b.*\b(performance|chart|trend)"))
        {
            intent.Type = QueryIntentType.MultiYearPerformance;
            _logger.LogInformation("Query intent matched: MultiYearPerformance");
        }
        // Check for standard deviation queries
        else if (Regex.IsMatch(queryLower, @"\b(standard\s+deviation|std\s+dev|stddev|variance|variability|deviation)\b"))
        {
            intent.Type = QueryIntentType.StandardDeviation;
            _logger.LogInformation("Query intent matched: StandardDeviation");
        }
        // Check for year-wise comparison queries
        else if (Regex.IsMatch(queryLower, @"\b(year\s*wise|yearly|year-wise|year\s+by\s+year|annual)\s+(comparison|compare|analysis|trend)") ||
                 Regex.IsMatch(queryLower, @"\b(compare|comparison)\s+.*\s+(year|years|annually)"))
        {
            intent.Type = QueryIntentType.YearWiseComparison;
            _logger.LogInformation("Query intent matched: YearWiseComparison");
        }
        // Check for "all markets" queries - these should trigger comparison
        else if (Regex.IsMatch(queryLower, @"\b(all|each|every)\s+(market|markets)\b"))
        {
            intent.Type = QueryIntentType.CompareMarkets;
            _logger.LogInformation("Query intent matched: CompareMarkets");
        }
        // Check for superlative/comparative queries about markets (most, best, worst, cheapest, etc.)
        else if (Regex.IsMatch(queryLower, @"\b(most|best|worst|cheapest|expensive|stable|volatile|safest|riskiest)\s+(market|markets)\b") ||
                 Regex.IsMatch(queryLower, @"\b(which|what)\s+market\s+(is|has)\s+(most|best|worst|cheapest|expensive|stable|volatile|safest|riskiest)\b") ||
                 Regex.IsMatch(queryLower, @"\b(show|tell|find)\s+(me\s+)?(the\s+)?(most|best|worst|cheapest|expensive|stable|volatile|safest|riskiest)\s+market\b"))
        {
            intent.Type = QueryIntentType.CompareMarkets;
            _logger.LogInformation("Query intent matched: CompareMarkets (superlative/comparative query)");
        }
        // Check for "show me" or "get" data queries - treat as insights
        else if (Regex.IsMatch(queryLower, @"\b(show|display|get|give|provide)\s+(me\s+)?.*\b(data|information|details)\b"))
        {
            intent.Type = QueryIntentType.GetInsights;
            _logger.LogInformation("Query intent matched: GetInsights (show/display pattern)");
        }
        // Check for "which month/year/day" questions - these are analysis questions
        else if (Regex.IsMatch(queryLower, @"\b(which|what)\s+(month|year|day|week|time|hour)\b.*\b(highest|lowest|maximum|minimum|most|least|best|worst)\b"))
            intent.Type = QueryIntentType.GetInsights;
        // Check for peak/max/min value queries (with optional words between)
        else if (Regex.IsMatch(queryLower, @"\b(peak|maximum|minimum|highest|lowest|max|min|average|mean|total|least|most|top|bottom|valley)\s+(\w+\s+)?(value|price|volume|mcp|mcv|mpp|mpv)\b"))
            intent.Type = QueryIntentType.GetInsights;
        else if (Regex.IsMatch(queryLower, @"\b(insights?|analysis|analyze|tell me about)\b"))
        {
            intent.Type = QueryIntentType.GetInsights;
            _logger.LogInformation("Query intent matched: GetInsights (insight/analysis pattern)");
        }
        else if (Regex.IsMatch(queryLower, @"\b(compare|comparison|versus|vs|which market)\b"))
            intent.Type = QueryIntentType.CompareMarkets;
        else if (Regex.IsMatch(queryLower, @"\b(forecast|predict|future|expect)\b"))
            intent.Type = QueryIntentType.Forecast;
        else if (Regex.IsMatch(queryLower, @"\b(buy|sell|recommend|should i|invest)\b"))
            intent.Type = QueryIntentType.BuySellRecommendation;
        else if (Regex.IsMatch(queryLower, @"\b(patterns?|anomal(y|ies)|unusual|spikes?|outliers?)\b"))
        {
            intent.Type = QueryIntentType.AnalyzePattern;
            _logger.LogInformation("Query intent matched: AnalyzePattern (anomaly/pattern/outlier query)");
        }
        // Check for cross-market comparison queries (GDAM>DAM, DAM>RTM, etc.)
        else if (Regex.IsMatch(queryLower, @"\b(when|identify|find|show|tell)\s+.*\s*(gdam|dam|rtm)\s*[>greater<less]\s*(gdam|dam|rtm)\b") ||
                 Regex.IsMatch(queryLower, @"\b(time\s+slots?|slots?|periods?)\s+when\s+(gdam|dam|rtm)\s*(>|greater|<|less)\s*(gdam|dam|rtm)\b"))
        {
            intent.Type = QueryIntentType.CrossMarketComparison;
            _logger.LogInformation("Query intent matched: CrossMarketComparison");
        }
        // Check for tariff range/bucket analysis queries
        else if (Regex.IsMatch(queryLower, @"\b(tariff|price)\s+(range|bucket|band|bracket)") ||
                 Regex.IsMatch(queryLower, @"\b(hitting|hit|fall|falling|within)\s+.*\s*(rs\.?|₹|rupees?)\s*\d") ||
                 Regex.IsMatch(queryLower, @"\b(hitting|hit|fall|falling|within)\s+.*\d+.*?(rs\.?|₹|rupees?)\b") || // Match "within 9-10Rs"
                 Regex.IsMatch(queryLower, @"\b(between|from)\s+\d+.*?(to|and).*?\d+.*?(rs\.?|₹|rupees?)\b") || // Match "between 9 and 10 Rs"
                 Regex.IsMatch(queryLower, @"\btotal\s+number\s+of\s+slots.*\s*%") ||
                 Regex.IsMatch(queryLower, @"\b(percentage|percent|%)\s+.*\s*(tariff|price|range)"))
        {
            intent.Type = QueryIntentType.TariffRangeAnalysis;
            _logger.LogInformation("Query intent matched: TariffRangeAnalysis");
        }
        // Check for time slot peak analysis queries
        else if (Regex.IsMatch(queryLower, @"\b(which|what)\s+(time\s+slots?|hours?|periods?)\s+.*\s+(higher|lower|peak|highest|lowest|expensive|cheaper)") ||
                 Regex.IsMatch(queryLower, @"\b(overall\s+time\s+slots?|peak\s+hours?|off-peak|peak\s+time)"))
        {
            intent.Type = QueryIntentType.TimeSlotPeakAnalysis;
            _logger.LogInformation("Query intent matched: TimeSlotPeakAnalysis");
        }
        // Check for trend-related questions (matches trend, trends, trending, etc.)
        else if (Regex.IsMatch(queryLower, @"\b(trend|increasing|decreasing)"))
            intent.Type = QueryIntentType.GetInsights;
        else
        {
            intent.Type = QueryIntentType.DataQuery;
            _logger.LogInformation("Query intent defaulted to: DataQuery (no pattern matched)");
        }

        // Extract market type
        if (queryLower.Contains("dam") && !queryLower.Contains("gdam"))
            intent.MarketType = "DAM";
        else if (queryLower.Contains("gdam"))
            intent.MarketType = "GDAM";
        else if (queryLower.Contains("rtm"))
            intent.MarketType = "RTM";

        // Extract comprehensive date/time information
        var dateInfo = DateTimeExtractor.ExtractDateTimeInfo(query);
        intent.StartDate = dateInfo.StartDate;
        intent.EndDate = dateInfo.EndDate;

        return intent;
    }

    private int ExtractForecastDays(string query)
    {
        // Extract number of days from query
        var match = Regex.Match(query, @"(\d+)[\s-]*(day|week|month)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var number = int.Parse(match.Groups[1].Value);
            var unit = match.Groups[2].Value.ToLower();

            return unit switch
            {
                "week" => number * 7,
                "month" => number * 30,
                _ => number
            };
        }

        return 30; // Default
    }

    private (bool HasTimeSlots, List<string> TimeSlots, string Metric) ExtractTimeSlotRange(string query)
    {
        var queryLower = query.ToLower();
        var timeSlots = new List<string>();

        // Check for MCP or MCV
        var metric = queryLower.Contains("mcv") ? "MCV" : "MCP";

        // Extract time range: "5PM to 10PM", "17:00 to 22:00", "5pm-10pm", etc.
        var timeRangeMatch = Regex.Match(query, @"(\d{1,2})(?::00)?\s*(pm|am)?\s*(?:to|-)\s*(\d{1,2})(?::00)?\s*(pm|am)?", RegexOptions.IgnoreCase);

        if (timeRangeMatch.Success)
        {
            int startHour = int.Parse(timeRangeMatch.Groups[1].Value);
            int endHour = int.Parse(timeRangeMatch.Groups[3].Value);
            string? startPeriod = timeRangeMatch.Groups[2].Value;
            string? endPeriod = timeRangeMatch.Groups[4].Value;

            // Convert to 24-hour format if PM/AM specified
            if (!string.IsNullOrEmpty(startPeriod) && startPeriod.ToLower() == "pm" && startHour < 12)
                startHour += 12;
            if (!string.IsNullOrEmpty(startPeriod) && startPeriod.ToLower() == "am" && startHour == 12)
                startHour = 0;
            if (!string.IsNullOrEmpty(endPeriod) && endPeriod.ToLower() == "pm" && endHour < 12)
                endHour += 12;
            if (!string.IsNullOrEmpty(endPeriod) && endPeriod.ToLower() == "am" && endHour == 12)
                endHour = 0;

            // Generate time slots (excluding end hour as it's typically a range)
            for (int hour = startHour; hour <= endHour; hour++)
            {
                timeSlots.Add($"{hour:D2}:00");
            }

            return (true, timeSlots, metric);
        }

        return (false, timeSlots, metric);
    }

    private IntelligentResponse GenerateTimeSlotForecastResponse(string query, QueryIntent intent, (bool HasTimeSlots, List<string> TimeSlots, string Metric) timeSlotInfo)
    {
        var sb = new StringBuilder();

        // Extract metric (MCP or MCV)
        var metric = timeSlotInfo.Metric;

        // Determine target date range (default to November 2025)
        var targetStartDate = intent.StartDate ?? new DateTime(2025, 11, 1);
        var targetEndDate = intent.EndDate ?? new DateTime(2025, 12, 1);

        // Determine markets to forecast (default to all 3)
        var markets = string.IsNullOrEmpty(intent.MarketType)
            ? new[] { "DAM", "GDAM", "RTM" }
            : new[] { intent.MarketType };

        var allForecasts = new Dictionary<string, List<TimeSlotForecastResult>>();

        // Generate forecasts for each market
        foreach (var market in markets)
        {
            var forecasts = _insightsEngine.GenerateTimeSlotForecasts(
                market,
                metric,
                timeSlotInfo.TimeSlots,
                targetStartDate,
                targetEndDate,
                historicalDaysToUse: 90);

            allForecasts[market] = forecasts;
        }

        // Build response
        var timeRangeStr = timeSlotInfo.TimeSlots.Any()
            ? $"{timeSlotInfo.TimeSlots.First()} to {timeSlotInfo.TimeSlots.Last()}"
            : "specified time range";

        sb.AppendLine($"# 🔮 {metric} Forecast for Time Slots {timeRangeStr}\n");
        sb.AppendLine($"**Period**: {targetStartDate:MMMM dd, yyyy} to {targetEndDate.AddDays(-1):MMMM dd, yyyy}\n");

        foreach (var marketEntry in allForecasts)
        {
            var market = marketEntry.Key;
            var forecasts = marketEntry.Value;

            sb.AppendLine($"## {market} Market\n");

            if (!forecasts.Any())
            {
                sb.AppendLine("Insufficient historical data to generate reliable forecasts for these time slots.\n");
                continue;
            }

            foreach (var forecast in forecasts)
            {
                sb.AppendLine($"### Time Slot: {forecast.TimeBlock}\n");
                sb.AppendLine($"- **Historical Average ({metric})**: {(forecast.Metric == "MCV" ? $"{forecast.AverageHistorical:F2} GW" : $"₹{forecast.AverageHistorical:F2}/kWh")}");
                sb.AppendLine($"- **Confidence**: {forecast.ConfidenceScore:P0}\n");

                if (forecast.Predictions.Any())
                {
                    // Show weekly summary
                    var firstWeekPredictions = forecast.Predictions.Take(7).ToList();
                    var avgPredicted = firstWeekPredictions.Average(p => p.PredictedValue);

                    sb.AppendLine($"**First Week Forecast** ({firstWeekPredictions.First().Date:MMM dd} - {firstWeekPredictions.Last().Date:MMM dd}):");
                    sb.AppendLine($"- Average Predicted: {(forecast.Metric == "MCV" ? $"{avgPredicted:F2} GW" : $"₹{avgPredicted:F2}/kWh")}");
                    sb.AppendLine($"- Range: {(forecast.Metric == "MCV" ? $"{firstWeekPredictions.Min(p => p.LowerBound):F2} - {firstWeekPredictions.Max(p => p.UpperBound):F2} GW" : $"₹{firstWeekPredictions.Min(p => p.LowerBound):F2} - ₹{firstWeekPredictions.Max(p => p.UpperBound):F2}/kWh")}");

                    var change = avgPredicted - forecast.AverageHistorical;
                    var changePercent = forecast.AverageHistorical > 0 ? (change / forecast.AverageHistorical) * 100 : 0;
                    sb.AppendLine($"- Change from Historical: {(changePercent >= 0 ? "+" : "")}{changePercent:F2}%\n");
                }
            }

            sb.AppendLine();
        }

        // Add business implications
        sb.AppendLine($"## 💡 Business Implications\n");
        sb.AppendLine($"These forecasts show expected {metric} values during the {timeRangeStr} time slots.");
        sb.AppendLine($"Use this information to optimize energy trading strategies during these peak/off-peak hours.\n");

        var response = new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString(),
            KeyFindings = new List<string>(),
            Charts = new List<ChartData>(),
            Data = new Dictionary<string, object>
            {
                ["forecasts"] = allForecasts,
                ["metric"] = metric,
                ["timeSlots"] = timeSlotInfo.TimeSlots
            }
        };

        return response;
    }

    private string GetVolatilityDescription(decimal volatility)
    {
        return volatility switch
        {
            < 5 => "Very Low",
            < 10 => "Low",
            < 15 => "Moderate",
            < 25 => "High",
            _ => "Very High"
        };
    }

    /// <summary>
    /// Create price and volume charts from insights data
    /// </summary>
    private List<ChartData> CreateInsightsCharts(MarketInsights insights, QueryIntent intent)
    {
        var charts = new List<ChartData>();

        // Get the actual data for the period
        var data = _dataService.GetAllData();

        if (!string.IsNullOrEmpty(intent.MarketType))
        {
            data = data.Where(d => d.Type.Equals(intent.MarketType, StringComparison.OrdinalIgnoreCase));
        }

        if (intent.StartDate.HasValue)
        {
            data = data.Where(d => d.Date >= intent.StartDate.Value);
        }

        if (intent.EndDate.HasValue)
        {
            data = data.Where(d => d.Date < intent.EndDate.Value);
        }

        var dataList = data.OrderBy(d => d.Date).ThenBy(d => d.TimeBlock).ToList();

        if (!dataList.Any()) return charts;

        // Determine grouping based on date range
        var dateRange = (intent.EndDate ?? DateTime.Today) - (intent.StartDate ?? dataList.Min(d => d.Date));
        var daysDiff = (int)dateRange.TotalDays;

        List<string> labels;
        List<decimal> prices;
        List<decimal> volumes;

        if (daysDiff <= 1)
        {
            // Single day - group by time block
            var grouped = dataList.GroupBy(d => d.TimeBlock).OrderBy(g => g.Key).ToList();
            labels = grouped.Select(g => g.Key.Substring(0, 5)).ToList(); // "HH:MM"
            prices = grouped.Select(g => g.Average(d => d.MCP)).ToList();
            volumes = grouped.Select(g => g.Average(d => d.MCV)).ToList();
        }
        else if (daysDiff <= 60)
        {
            // Up to 60 days - group by day
            var grouped = dataList.GroupBy(d => d.Date.ToString("yyyy-MM-dd")).OrderBy(g => g.Key).ToList();
            labels = grouped.Select(g => DateTime.Parse(g.Key).ToString("MMM dd")).ToList();
            prices = grouped.Select(g => g.Average(d => d.MCP)).ToList();
            volumes = grouped.Select(g => g.Average(d => d.MCV)).ToList();
        }
        else
        {
            // More than 60 days - group by week
            var grouped = dataList.GroupBy(d =>
            {
                var weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar
                    .GetWeekOfYear(d.Date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                return $"{d.Date.Year}-W{weekOfYear:D2}";
            }).OrderBy(g => g.Key).ToList();
            labels = grouped.Select(g => g.Key).ToList();
            prices = grouped.Select(g => g.Average(d => d.MCP)).ToList();
            volumes = grouped.Select(g => g.Average(d => d.MCV)).ToList();
        }

        // Create MCP (Price) Chart
        charts.Add(new ChartData
        {
            ChartType = "Line",
            Title = $"{intent.MarketType ?? "Market"} - Price Trend (MCP)",
            Labels = labels,
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = "MCP (₹/kWh)",
                    Data = prices.Select(p => (decimal)p).ToList(),
                    Color = "rgba(54, 162, 235, 0.2)",
                    BorderColor = "rgba(54, 162, 235, 1)",
                    Fill = true,
                    Tension = 0.4m
                }
            }
        });

        // Create MCV (Volume) Chart
        charts.Add(new ChartData
        {
            ChartType = "Bar",
            Title = $"{intent.MarketType ?? "Market"} - Volume Trend (MCV)",
            Labels = labels,
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = "MCV (GW)",
                    Data = volumes.Select(v => (decimal)v).ToList(),
                    Color = "rgba(75, 192, 192, 0.6)",
                    BorderColor = "rgba(75, 192, 192, 1)",
                    Fill = false,
                    Tension = 0m
                }
            }
        });

        return charts;
    }

    private List<ChartData> CreateTariffRangeCharts(List<TariffRangeBucket> results, string metric)
    {
        var charts = new List<ChartData>();

        // Get unique markets and ranges
        var markets = results.Select(r => r.MarketType).Distinct().OrderBy(m => m).ToList();
        var ranges = results.Where(r => r.MarketType == markets.First())
                           .OrderBy(r => r.MinValue)
                           .Select(r => r.RangeDescription)
                           .ToList();

        // Define colors for each range - matching professional theme
        var colors = new List<string>
        {
            "rgba(15, 76, 129, 0.8)",     // Primary Blue
            "rgba(26, 127, 184, 0.8)",    // Secondary Blue
            "rgba(0, 168, 204, 0.8)",     // Accent Cyan
            "rgba(56, 189, 248, 0.8)",    // Light Blue
            "rgba(20, 184, 166, 0.8)",    // Teal
            "rgba(16, 185, 129, 0.8)",    // Emerald
            "rgba(251, 146, 60, 0.8)"     // Amber (for contrast)
        };

        var borderColors = colors.Select(c => c.Replace("0.8", "1")).ToList();

        // Create stacked bar chart - Percentage Distribution
        var percentageDatasets = new List<ChartDataset>();
        for (int i = 0; i < ranges.Count; i++)
        {
            var rangeDesc = ranges[i];
            percentageDatasets.Add(new ChartDataset
            {
                Label = rangeDesc,
                Data = markets.Select(m =>
                {
                    var bucket = results.FirstOrDefault(r => r.MarketType == m && r.RangeDescription == rangeDesc);
                    return bucket?.Percentage ?? 0;
                }).ToList(),
                Color = colors[i % colors.Count],
                BorderColor = borderColors[i % borderColors.Count]
            });
        }

        var chartTitle = metric == "MCV" ? "Volume Range Distribution by Market (%)" : "Tariff Range Distribution by Market (%)";
        charts.Add(new ChartData
        {
            ChartType = "Bar",
            Title = chartTitle,
            Labels = markets,
            Datasets = percentageDatasets
        });

        // Create slot count comparison chart
        var slotCountDatasets = new List<ChartDataset>();
        for (int i = 0; i < ranges.Count; i++)
        {
            var rangeDesc = ranges[i];
            slotCountDatasets.Add(new ChartDataset
            {
                Label = rangeDesc,
                Data = markets.Select(m =>
                {
                    var bucket = results.FirstOrDefault(r => r.MarketType == m && r.RangeDescription == rangeDesc);
                    return (decimal)(bucket?.SlotCount ?? 0);
                }).ToList(),
                Color = colors[i % colors.Count],
                BorderColor = borderColors[i % borderColors.Count]
            });
        }

        var slotChartTitle = metric == "MCV" ? "Volume Range Slot Counts by Market" : "Tariff Range Slot Counts by Market";
        charts.Add(new ChartData
        {
            ChartType = "Bar",
            Title = slotChartTitle,
            Labels = markets,
            Datasets = slotCountDatasets
        });

        return charts;
    }

    private List<ChartData> CreateTimeSlotPeakCharts(List<TimeSlotPeakAnalysis> results, string metric)
    {
        var charts = new List<ChartData>();

        var unit = metric == "MCV" ? "GW" : "₹/kWh";
        var metricLabel = metric == "MCV" ? "Volume" : "Price";

        // Create line chart showing average values across all time slots for each market
        var markets = results.Select(r => r.MarketType).ToList();
        var colors = new Dictionary<string, string>
        {
            ["DAM"] = "rgba(15, 76, 129, 0.8)",    // Primary Blue
            ["GDAM"] = "rgba(0, 168, 204, 0.8)",   // Accent Cyan
            ["RTM"] = "rgba(20, 184, 166, 0.8)"    // Teal
        };

        var borderColors = new Dictionary<string, string>
        {
            ["DAM"] = "rgba(15, 76, 129, 1)",    // Primary Blue
            ["GDAM"] = "rgba(0, 168, 204, 1)",   // Accent Cyan
            ["RTM"] = "rgba(20, 184, 166, 1)"    // Teal
        };

        // Get all unique time blocks (sorted)
        var allTimeBlocks = results.SelectMany(r => r.TimeSlots.Select(ts => ts.TimeBlock))
                                  .Distinct()
                                  .OrderBy(tb => tb)
                                  .ToList();

        // Create dataset for each market
        var lineDatasets = new List<ChartDataset>();
        foreach (var market in markets)
        {
            var analysis = results.First(r => r.MarketType == market);
            lineDatasets.Add(new ChartDataset
            {
                Label = $"{market} Avg {metricLabel}",
                Data = allTimeBlocks.Select(tb =>
                {
                    var slot = analysis.TimeSlots.FirstOrDefault(ts => ts.TimeBlock == tb);
                    return slot?.AverageMCP ?? 0;
                }).ToList(),
                Color = colors.GetValueOrDefault(market, "rgba(15, 76, 129, 0.8)"),
                BorderColor = borderColors.GetValueOrDefault(market, "rgba(15, 76, 129, 1)"),
                Fill = false,
                Tension = 0.4m
            });
        }

        charts.Add(new ChartData
        {
            ChartType = "Line",
            Title = $"Average {metricLabel} by Time Slot Across Markets",
            Labels = allTimeBlocks,
            Datasets = lineDatasets
        });

        // Create bar chart showing top 10 peak time slots for each market
        foreach (var analysis in results)
        {
            var topSlots = analysis.TimeSlots.OrderByDescending(ts => ts.AverageMCP).Take(10).ToList();

            charts.Add(new ChartData
            {
                ChartType = "Bar",
                Title = $"{analysis.MarketType} - Top 10 Time Slots by Average {metricLabel}",
                Labels = topSlots.Select(ts => ts.TimeBlock).ToList(),
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = $"Average {metricLabel} ({unit})",
                        Data = topSlots.Select(ts => ts.AverageMCP).ToList(),
                        Color = colors.GetValueOrDefault(analysis.MarketType, "rgba(15, 76, 129, 0.8)"),
                        BorderColor = borderColors.GetValueOrDefault(analysis.MarketType, "rgba(15, 76, 129, 1)")
                    }
                }
            });
        }

        return charts;
    }

    private ChartData CreateForecastChart(ForecastResult forecast)
    {
        return new ChartData
        {
            ChartType = "Line",
            Title = $"{forecast.MarketType} - {forecast.Metric} Forecast",
            Labels = forecast.Predictions.Select(p => p.Date.ToString("MMM dd")).ToList(),
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = "Predicted Value (₹/kWh)",
                    Data = forecast.Predictions.Select(p => p.PredictedValue).ToList(),
                    Color = "rgba(102, 126, 234, 0.2)",
                    BorderColor = "rgba(102, 126, 234, 1)",
                    Fill = true,
                    Tension = 0.4m
                },
                new ChartDataset
                {
                    Label = "Upper Bound",
                    Data = forecast.Predictions.Select(p => p.UpperBound).ToList(),
                    Color = "rgba(255, 99, 132, 0.1)",
                    BorderColor = "rgba(255, 99, 132, 0.5)",
                    Fill = false,
                    Tension = 0.4m
                },
                new ChartDataset
                {
                    Label = "Lower Bound",
                    Data = forecast.Predictions.Select(p => p.LowerBound).ToList(),
                    Color = "rgba(75, 192, 192, 0.1)",
                    BorderColor = "rgba(75, 192, 192, 0.5)",
                    Fill = false,
                    Tension = 0.4m
                }
            }
        };
    }

    /// <summary>
    /// Convert present tense descriptions to past tense for historical data analysis
    /// </summary>
    private string ConvertToPastTense(string description)
    {
        // Common present tense to past tense conversions for market analysis
        var conversions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { " is increasing", " was increasing" },
            { " is decreasing", " was decreasing" },
            { " is stable", " was stable" },
            { " is rising", " was rising" },
            { " is falling", " was falling" },
            { " is growing", " was growing" },
            { " is declining", " was declining" },
            { " is trending", " was trending" },
            { " is moving", " was moving" },
            { " shows ", " showed " },
            { " indicates ", " indicated " },
            { " suggests ", " suggested " },
            { " demonstrates ", " demonstrated " },
            { " exhibits ", " exhibited " },
            { " occurs ", " occurred " },
            { " happens ", " happened " },
            { " typically occurs", " typically occurred" },
            { " typically seen", " typically seen" }  // This is already correct for past
        };

        var result = description;
        foreach (var conversion in conversions)
        {
            result = Regex.Replace(result, conversion.Key, conversion.Value, RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Generate multi-year performance response
    /// </summary>
    private IntelligentResponse GenerateMultiYearPerformanceResponse(string query, QueryIntent intent)
    {
        var result = _advancedAnalytics.AnalyzeMultiYearPerformance(query).Result;

        var sb = new StringBuilder();
        sb.AppendLine($"## {result.Message}\n");
        sb.AppendLine($"Analyzing performance trends across **{result.YearlyData.Count} years** ({result.StartYear}-{result.EndYear}) for **{result.Markets.Count} market(s)**.\n");

        // Add summary table
        sb.AppendLine("### Performance Summary\n");
        foreach (var market in result.Markets)
        {
            var marketKey = market.ToString();
            sb.AppendLine($"#### {marketKey} Market\n");
            sb.AppendLine("| Year | Avg MCP (₹/kWh) | Avg MCV (GW) | Records |");
            sb.AppendLine("|-----:|----------------:|-------------:|--------:|");

            foreach (var yearData in result.YearlyData.OrderBy(kvp => kvp.Key))
            {
                if (yearData.Value.ContainsKey(marketKey))
                {
                    var data = yearData.Value[marketKey];
                    sb.AppendLine($"| {yearData.Key} | {data.avgMCP:F2} | {data.avgMCV:F2} | {data.count:N0} |");
                }
            }
            sb.AppendLine();
        }

        var response = new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString()
        };

        // Generate insights
        var keyFindings = new List<string>();
        var recommendations = new List<string>();

        foreach (var market in result.Markets)
        {
            var marketKey = market.ToString();
            var marketYearData = result.YearlyData
                .Where(kvp => kvp.Value.ContainsKey(marketKey))
                .OrderBy(kvp => kvp.Key)
                .ToList();

            if (marketYearData.Count >= 2)
            {
                // Calculate MCP trend
                var firstYear = marketYearData.First();
                var lastYear = marketYearData.Last();
                var mcpChange = lastYear.Value[marketKey].avgMCP - firstYear.Value[marketKey].avgMCP;
                var mcpChangePercent = (mcpChange / firstYear.Value[marketKey].avgMCP) * 100;

                // Calculate MCV trend
                var mcvChange = lastYear.Value[marketKey].avgMCV - firstYear.Value[marketKey].avgMCV;
                var mcvChangePercent = firstYear.Value[marketKey].avgMCV != 0
                    ? (mcvChange / firstYear.Value[marketKey].avgMCV) * 100
                    : 0;

                // Add findings
                var mcpTrend = mcpChange < 0 ? "decreased" : "increased";
                var mcvTrend = mcvChange < 0 ? "decreased" : "increased";

                keyFindings.Add($"**{marketKey}**: MCP {mcpTrend} by {Math.Abs(mcpChangePercent):F1}% ({firstYear.Value[marketKey].avgMCP:F2} to {lastYear.Value[marketKey].avgMCP:F2} ₹/kWh) from {firstYear.Key} to {lastYear.Key}");
                keyFindings.Add($"**{marketKey}**: MCV {mcvTrend} by {Math.Abs(mcvChangePercent):F1}% ({firstYear.Value[marketKey].avgMCV:F2} to {lastYear.Value[marketKey].avgMCV:F2} GW) indicating {(mcvChange > 0 ? "growing market activity" : "declining market volume")}");

                // Add recommendations
                if (mcpChange < 0 && mcvChange > 0)
                {
                    recommendations.Add($"**{marketKey}**: Favorable conditions detected - decreasing prices with increasing volume suggest competitive market pricing");
                }
                else if (mcpChange > 0 && mcvChange < 0)
                {
                    recommendations.Add($"**{marketKey}**: Caution advised - increasing prices with decreasing volume may indicate supply constraints");
                }
            }
        }

        response.KeyFindings = keyFindings;
        response.Recommendations = recommendations;

        // Create chart data for multi-year visualization
        var charts = new List<ChartData>();
        var labels = result.YearlyData.Keys.OrderBy(y => y).Select(y => y.ToString()).ToList();

        // Check if we have both metrics - if so, create combined chart with dual Y-axes
        var hasBothMetrics = result.Metrics.Contains("MCP") && result.Metrics.Contains("MCV");

        if (hasBothMetrics)
        {
            // Combined chart: MCP as bars (left Y-axis) + MCV as lines (right Y-axis)
            var datasets = new List<ChartDataset>();

            // Market colors - matching the theme
            var marketBarColors = new Dictionary<string, (string bg, string border)>
            {
                ["DAM"] = ("rgba(15, 76, 129, 0.8)", "rgba(15, 76, 129, 1)"),     // Primary Blue
                ["GDAM"] = ("rgba(0, 168, 204, 0.8)", "rgba(0, 168, 204, 1)"),    // Accent Cyan
                ["RTM"] = ("rgba(20, 184, 166, 0.8)", "rgba(20, 184, 166, 1)")    // Teal
            };

            var marketLineColors = new Dictionary<string, (string bg, string border)>
            {
                ["DAM"] = ("rgba(239, 68, 68, 0.2)", "rgba(239, 68, 68, 1)"),      // Red
                ["GDAM"] = ("rgba(168, 85, 247, 0.2)", "rgba(168, 85, 247, 1)"),   // Purple
                ["RTM"] = ("rgba(34, 197, 94, 0.2)", "rgba(34, 197, 94, 1)")       // Green
            };

            // Add MCP datasets (bars) - on left Y-axis
            foreach (var market in result.Markets)
            {
                var marketKey = market.ToString();
                var mcpValues = result.YearlyData
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp =>
                    {
                        if (kvp.Value.ContainsKey(marketKey))
                            return kvp.Value[marketKey].avgMCP;
                        return 0m;
                    })
                    .ToList();

                var colors = marketBarColors.ContainsKey(marketKey)
                    ? marketBarColors[marketKey]
                    : ("rgba(100, 116, 139, 0.8)", "rgba(100, 116, 139, 1)");

                datasets.Add(new ChartDataset
                {
                    Label = $"{marketKey} - MCP (Price)",
                    Data = mcpValues,
                    Type = "bar",
                    BackgroundColor = colors.Item1,
                    BorderColor = colors.Item2,
                    BorderWidth = 2,
                    YAxisID = "y-mcp",
                    Order = 2  // Draw bars first (on bottom)
                });
            }

            // Add MCV datasets (lines) - on right Y-axis
            foreach (var market in result.Markets)
            {
                var marketKey = market.ToString();
                var mcvValues = result.YearlyData
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp =>
                    {
                        if (kvp.Value.ContainsKey(marketKey))
                            return kvp.Value[marketKey].avgMCV;
                        return 0m;
                    })
                    .ToList();

                var colors = marketLineColors.ContainsKey(marketKey)
                    ? marketLineColors[marketKey]
                    : ("rgba(100, 116, 139, 0.2)", "rgba(100, 116, 139, 1)");

                datasets.Add(new ChartDataset
                {
                    Label = $"{marketKey} - MCV (Volume)",
                    Data = mcvValues,
                    Type = "line",
                    BackgroundColor = colors.Item1,
                    BorderColor = colors.Item2,
                    BorderWidth = 3,
                    Fill = false,
                    Tension = 0.4m,
                    YAxisID = "y-mcv",
                    Order = 1  // Draw lines on top
                });
            }

            charts.Add(new ChartData
            {
                Title = $"Multi-Year Performance: MCP (Price) & MCV (Volume) Trends ({result.StartYear}-{result.EndYear})",
                Labels = labels,
                Datasets = datasets,
                ChartType = "combined_bar_line",
                Has_Dual_Axes = true  // Enable dual Y-axis rendering
            });
        }
        else
        {
            // Single metric - create simple chart
            foreach (var metric in result.Metrics)
            {
                var datasets = new List<ChartDataset>();

                foreach (var market in result.Markets)
                {
                    var marketKey = market.ToString();
                    var values = result.YearlyData
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp =>
                        {
                            if (kvp.Value.ContainsKey(marketKey))
                            {
                                return metric == "MCP" ? kvp.Value[marketKey].avgMCP : kvp.Value[marketKey].avgMCV;
                            }
                            return 0m;
                        })
                        .ToList();

                    datasets.Add(new ChartDataset
                    {
                        Label = $"{marketKey} - {metric}",
                        Data = values
                    });
                }

                var unit = metric == "MCP" ? "(₹/kWh)" : "(GW)";
                charts.Add(new ChartData
                {
                    Title = $"{metric} Performance Trends {unit} ({result.StartYear}-{result.EndYear})",
                    Labels = labels,
                    Datasets = datasets,
                    ChartType = metric == "MCP" ? "bar" : "line"
                });
            }
        }

        response.Charts = charts;
        response.Success = true;
        response.Message = result.Message;
        return response;
    }

    /// <summary>
    /// Generate standard deviation response
    /// </summary>
    private IntelligentResponse GenerateStandardDeviationResponse(string query, QueryIntent intent)
    {
        var result = _advancedAnalytics.AnalyzeStandardDeviation(query).Result;

        var sb = new StringBuilder();
        sb.AppendLine($"## {result.Message}\n");
        sb.AppendLine($"**Market:** {result.Market}");
        sb.AppendLine($"**Metric:** {result.Metric}\n");

        // Add statistical summary table
        sb.AppendLine("| Period | Mean | Std Dev | Variance | Min | Max | Data Points |");
        sb.AppendLine("|:-------|-----:|--------:|---------:|----:|----:|------------:|");

        foreach (var period in result.Periods)
        {
            var unit = result.Metric == "MCP" ? "₹/kWh" : "GW";
            sb.AppendLine($"| **{period.PeriodName}** | {period.Mean:F2} {unit} | {period.StandardDeviation:F2} | {period.Variance:F2} | {period.Min:F2} | {period.Max:F2} | {period.DataPoints:N0} |");
        }

        sb.AppendLine();

        var response = new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString()
        };

        // Create chart showing standard deviation comparison
        var chart = new ChartData
        {
            Title = $"Standard Deviation Comparison - {result.Market} {result.Metric}",
            Labels = result.Periods.Select(p => p.PeriodName).ToList(),
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = "Mean",
                    Data = result.Periods.Select(p => p.Mean).ToList()
                },
                new ChartDataset
                {
                    Label = "Standard Deviation",
                    Data = result.Periods.Select(p => p.StandardDeviation).ToList()
                }
            },
            ChartType = "bar"
        };

        response.Charts = new List<ChartData> { chart };
        response.Success = true;
        response.Message = result.Message;
        return response;
    }

    /// <summary>
    /// Generate year-wise comparison response
    /// </summary>
    private IntelligentResponse GenerateYearWiseComparisonResponse(string query, QueryIntent intent)
    {
        var result = _advancedAnalytics.AnalyzeYearWiseComparison(query).Result;

        var sb = new StringBuilder();
        sb.AppendLine($"## {result.Message}\n");

        // Create comparison tables for each market
        foreach (var market in result.Markets)
        {
            var marketKey = market.ToString();
            sb.AppendLine($"### {marketKey} Market\n");

            if (result.IncludeMCP && result.IncludeMCV)
            {
                sb.AppendLine("| Year | Avg MCP | Max MCP | Min MCP | Avg MCV | Max MCV | Min MCV | Records |");
                sb.AppendLine("|-----:|--------:|--------:|--------:|--------:|--------:|--------:|--------:|");

                foreach (var year in result.Years)
                {
                    if (result.ComparisonData.ContainsKey(year) && result.ComparisonData[year].ContainsKey(marketKey))
                    {
                        var data = result.ComparisonData[year][marketKey];
                        sb.AppendLine($"| **{year}** | {data.AvgMCP:F2} | {data.MaxMCP:F2} | {data.MinMCP:F2} | {data.AvgMCV:F2} | {data.MaxMCV:F2} | {data.MinMCV:F2} | {data.RecordCount:N0} |");
                    }
                }
            }
            else if (result.IncludeMCP)
            {
                sb.AppendLine("| Year | Avg MCP | Max MCP | Min MCP | Records |");
                sb.AppendLine("|-----:|--------:|--------:|--------:|--------:|");

                foreach (var year in result.Years)
                {
                    if (result.ComparisonData.ContainsKey(year) && result.ComparisonData[year].ContainsKey(marketKey))
                    {
                        var data = result.ComparisonData[year][marketKey];
                        sb.AppendLine($"| **{year}** | {data.AvgMCP:F2} | {data.MaxMCP:F2} | {data.MinMCP:F2} | {data.RecordCount:N0} |");
                    }
                }
            }
            else if (result.IncludeMCV)
            {
                sb.AppendLine("| Year | Avg MCV | Max MCV | Min MCV | Records |");
                sb.AppendLine("|-----:|--------:|--------:|--------:|--------:|");

                foreach (var year in result.Years)
                {
                    if (result.ComparisonData.ContainsKey(year) && result.ComparisonData[year].ContainsKey(marketKey))
                    {
                        var data = result.ComparisonData[year][marketKey];
                        sb.AppendLine($"| **{year}** | {data.AvgMCV:F2} | {data.MaxMCV:F2} | {data.MinMCV:F2} | {data.RecordCount:N0} |");
                    }
                }
            }

            sb.AppendLine();
        }

        var response = new IntelligentResponse
        {
            Query = query,
            Answer = sb.ToString()
        };

        // Create charts for year-wise comparison
        var charts = new List<ChartData>();

        if (result.IncludeMCP)
        {
            var mcpDatasets = new List<ChartDataset>();
            foreach (var market in result.Markets)
            {
                var marketKey = market.ToString();
                var values = result.Years
                    .Select(year =>
                    {
                        if (result.ComparisonData.ContainsKey(year) &&
                            result.ComparisonData[year].ContainsKey(marketKey))
                        {
                            return result.ComparisonData[year][marketKey].AvgMCP;
                        }
                        return 0m;
                    })
                    .ToList();

                mcpDatasets.Add(new ChartDataset
                {
                    Label = $"{marketKey} - MCP",
                    Data = values
                });
            }

            charts.Add(new ChartData
            {
                Title = "Year-wise MCP Comparison",
                Labels = result.Years.Select(y => y.ToString()).ToList(),
                Datasets = mcpDatasets,
                ChartType = "bar"
            });
        }

        if (result.IncludeMCV)
        {
            var mcvDatasets = new List<ChartDataset>();
            foreach (var market in result.Markets)
            {
                var marketKey = market.ToString();
                var values = result.Years
                    .Select(year =>
                    {
                        if (result.ComparisonData.ContainsKey(year) &&
                            result.ComparisonData[year].ContainsKey(marketKey))
                        {
                            return result.ComparisonData[year][marketKey].AvgMCV;
                        }
                        return 0m;
                    })
                    .ToList();

                mcvDatasets.Add(new ChartDataset
                {
                    Label = $"{marketKey} - MCV",
                    Data = values
                });
            }

            charts.Add(new ChartData
            {
                Title = "Year-wise MCV Comparison",
                Labels = result.Years.Select(y => y.ToString()).ToList(),
                Datasets = mcvDatasets,
                ChartType = "bar"
            });
        }

        response.Charts = charts;
        response.Success = true;
        response.Message = result.Message;
        return response;
    }
}

/// <summary>
/// Query intent classification
/// </summary>
public class QueryIntent
{
    public string OriginalQuery { get; set; } = string.Empty;
    public QueryIntentType Type { get; set; }
    public string? MarketType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string> RequestedMetrics { get; set; } = new List<string>();
    public Dictionary<string, string> ChartTypes { get; set; } = new Dictionary<string, string>(); // Metric -> ChartType
    public string? TimeSlotStart { get; set; }
    public string? TimeSlotEnd { get; set; }
}

/// <summary>
/// Types of query intents
/// </summary>
public enum QueryIntentType
{
    DataQuery,
    GetInsights,
    CompareMarkets,
    Forecast,
    BuySellRecommendation,
    AnalyzePattern,
    CrossMarketComparison,
    TariffRangeAnalysis,
    TimeSlotPeakAnalysis,
    CustomChartRequest,
    MultiYearPerformance,
    StandardDeviation,
    YearWiseComparison
}
