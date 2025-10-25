using IEXInsiderMCP.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Advanced analytics engine for generating insights, forecasts, and recommendations
/// </summary>
public class InsightsEngine
{
    private readonly IEXDataService _dataService;
    private readonly ILogger<InsightsEngine> _logger;
    private readonly MLContext _mlContext;

    public InsightsEngine(IEXDataService dataService, ILogger<InsightsEngine> logger)
    {
        _dataService = dataService;
        _logger = logger;
        _mlContext = new MLContext(seed: 1);
    }

    /// <summary>
    /// Generate comprehensive market insights
    /// </summary>
    public MarketInsights GenerateInsights(InsightsRequest request)
    {
        _logger.LogInformation("Generating insights for market: {MarketType}", request.MarketType ?? "ALL");

        var data = GetFilteredData(request);

        var insights = new MarketInsights
        {
            MarketType = request.MarketType ?? "ALL",
            AnalysisDate = DateTime.UtcNow,
            DataPointsAnalyzed = data.Count
        };

        // Analyze prices
        insights.PriceAnalysis = AnalyzePrices(data);

        // Analyze volumes
        insights.VolumeAnalysis = AnalyzeVolumes(data);

        // Detect trends
        insights.Trends = DetectTrends(data);

        // Find anomalies
        insights.Anomalies = DetectAnomalies(data);

        // Identify patterns
        insights.Patterns = IdentifyPatterns(data);

        // Generate forecast
        if (request.IncludeForecasting && data.Count > 30)
        {
            insights.Forecast = GenerateForecast(data, request.ForecastDays, request.MarketType ?? "DAM");
        }

        // Create business recommendations
        if (request.IncludeRecommendations)
        {
            insights.Recommendations = GenerateRecommendations(insights, data);
        }

        return insights;
    }

    /// <summary>
    /// Analyze price trends and statistics
    /// </summary>
    private PriceInsights AnalyzePrices(List<IEXMarketData> data)
    {
        var prices = data.Select(d => d.MCP).ToList();
        var recent30Days = data.OrderByDescending(d => d.Date).Take(30 * 96).Select(d => d.MCP).ToList();
        var historical = data.Take(data.Count - recent30Days.Count).Select(d => d.MCP).ToList();

        var currentAvg = recent30Days.Any() ? recent30Days.Average() : 0;
        var historicalAvg = historical.Any() ? historical.Average() : currentAvg;

        var stdDev = CalculateStandardDeviation(prices);
        var volatility = CalculateVolatility(recent30Days);

        // Find peak and lowest price times
        var maxPrice = data.OrderByDescending(d => d.MCP).FirstOrDefault();
        var minPrice = data.OrderBy(d => d.MCP).FirstOrDefault();

        // Determine trend
        var trend = DetermineTrend(recent30Days);

        return new PriceInsights
        {
            CurrentAverage = currentAvg,
            HistoricalAverage = historicalAvg,
            PercentageChange = historicalAvg > 0 ? ((currentAvg - historicalAvg) / historicalAvg) * 100 : 0,
            Volatility = volatility,
            StandardDeviation = stdDev,
            PeakPriceTime = maxPrice != null ? new TimeSlot
            {
                Date = maxPrice.Date,
                TimeBlock = maxPrice.TimeBlock,
                Value = maxPrice.MCP
            } : new TimeSlot(),
            LowestPriceTime = minPrice != null ? new TimeSlot
            {
                Date = minPrice.Date,
                TimeBlock = minPrice.TimeBlock,
                Value = minPrice.MCP
            } : new TimeSlot(),
            Trend = trend
        };
    }

    /// <summary>
    /// Analyze volume trends and statistics
    /// </summary>
    private VolumeInsights AnalyzeVolumes(List<IEXMarketData> data)
    {
        var volumes = data.Select(d => d.MCV).ToList();
        var recent30Days = data.OrderByDescending(d => d.Date).Take(30 * 96).Select(d => d.MCV).ToList();
        var historical = data.Take(data.Count - recent30Days.Count).Select(d => d.MCV).ToList();

        var currentAvg = recent30Days.Any() ? recent30Days.Average() : 0;
        var historicalAvg = historical.Any() ? historical.Average() : currentAvg;

        var maxVolume = data.OrderByDescending(d => d.MCV).FirstOrDefault();
        var trend = DetermineTrend(recent30Days);

        return new VolumeInsights
        {
            CurrentAverage = currentAvg,
            HistoricalAverage = historicalAvg,
            PercentageChange = historicalAvg > 0 ? ((currentAvg - historicalAvg) / historicalAvg) * 100 : 0,
            PeakVolume = volumes.Max(),
            PeakVolumeTime = maxVolume != null ? new TimeSlot
            {
                Date = maxVolume.Date,
                TimeBlock = maxVolume.TimeBlock,
                Value = maxVolume.MCV
            } : new TimeSlot(),
            Trend = trend
        };
    }

    /// <summary>
    /// Detect market trends
    /// </summary>
    private List<TrendInsight> DetectTrends(List<IEXMarketData> data)
    {
        var trends = new List<TrendInsight>();

        // Price trend
        var prices = data.OrderBy(d => d.Date).Select(d => (double)d.MCP).ToList();
        var priceSlope = CalculateTrendSlope(prices);
        var priceStrength = Math.Min(Math.Abs(priceSlope) * 100, 100);

        trends.Add(new TrendInsight
        {
            Type = "Price",
            Direction = priceSlope > 0.01m ? "Upward" : priceSlope < -0.01m ? "Downward" : "Sideways",
            Strength = (decimal)priceStrength,
            Description = GenerateTrendDescription("Price", priceSlope, priceStrength),
            ConfidenceScore = CalculateConfidence(data.Count, priceStrength)
        });

        // Volume trend
        var volumes = data.OrderBy(d => d.Date).Select(d => (double)d.MCV).ToList();
        var volumeSlope = CalculateTrendSlope(volumes);
        var volumeStrength = Math.Min(Math.Abs(volumeSlope) * 100, 100);

        trends.Add(new TrendInsight
        {
            Type = "Volume",
            Direction = volumeSlope > 0.01m ? "Upward" : volumeSlope < -0.01m ? "Downward" : "Sideways",
            Strength = (decimal)volumeStrength,
            Description = GenerateTrendDescription("Volume", volumeSlope, volumeStrength),
            ConfidenceScore = CalculateConfidence(data.Count, volumeStrength)
        });

        return trends;
    }

    /// <summary>
    /// Detect anomalies in the data
    /// </summary>
    private List<AnomalyInsight> DetectAnomalies(List<IEXMarketData> data)
    {
        var anomalies = new List<AnomalyInsight>();

        if (!data.Any())
        {
            return anomalies;
        }

        var prices = data.Select(d => d.MCP).ToList();
        var mean = prices.Average();
        var stdDev = CalculateStandardDeviation(prices);

        // If standard deviation is 0 (all values are the same), no anomalies to detect
        if (stdDev == 0)
        {
            return anomalies;
        }

        // Find price anomalies (values beyond 3 standard deviations)
        var threshold = 3m;
        foreach (var record in data)
        {
            var zScore = Math.Abs((record.MCP - mean) / stdDev);
            if (zScore > threshold)
            {
                anomalies.Add(new AnomalyInsight
                {
                    Date = record.Date,
                    TimeBlock = record.TimeBlock,
                    Type = record.MCP > mean ? "PriceSpike" : "PriceDrop",
                    ExpectedValue = mean,
                    ActualValue = record.MCP,
                    Deviation = (record.MCP - mean) / mean * 100,
                    Description = $"Unusual {(record.MCP > mean ? "high" : "low")} price detected: ₹{record.MCP:F2}/kWh (expected ~₹{mean:F2}/kWh)",
                    Severity = zScore > 4m ? "High" : zScore > 3.5m ? "Medium" : "Low"
                });
            }
        }

        // Limit to top 10 most significant anomalies
        return anomalies.OrderByDescending(a => Math.Abs(a.Deviation)).Take(10).ToList();
    }

    /// <summary>
    /// Identify recurring patterns
    /// </summary>
    private List<PatternInsight> IdentifyPatterns(List<IEXMarketData> data)
    {
        var patterns = new List<PatternInsight>();

        if (!data.Any())
        {
            return patterns;
        }

        // Daily pattern - group by hour
        var hourlyPrices = data.GroupBy(d => int.Parse(d.TimeBlock.Split(':')[0]))
            .Select(g => new { Hour = g.Key, AvgPrice = g.Average(x => x.MCP) })
            .OrderBy(x => x.Hour)
            .ToList();

        var peakHours = hourlyPrices.OrderByDescending(h => h.AvgPrice).Take(3).Select(h => $"{h.Hour:D2}:00").ToList();
        var offPeakHours = hourlyPrices.OrderBy(h => h.AvgPrice).Take(3).Select(h => $"{h.Hour:D2}:00").ToList();

        patterns.Add(new PatternInsight
        {
            PatternType = "Daily",
            Description = $"Peak pricing typically occurs during {string.Join(", ", peakHours)}. Lowest prices during {string.Join(", ", offPeakHours)}.",
            TimeWindows = peakHours,
            Confidence = 0.85m
        });

        // Weekly pattern - group by day of week
        if (data.Select(d => d.Date.Date).Distinct().Count() > 14)
        {
            var weeklyPrices = data.GroupBy(d => d.Date.DayOfWeek)
                .Select(g => new { Day = g.Key, AvgPrice = g.Average(x => x.MCP) })
                .OrderByDescending(x => x.AvgPrice)
                .ToList();

            var highDemandDays = weeklyPrices.Take(2).Select(d => d.Day.ToString()).ToList();

            patterns.Add(new PatternInsight
            {
                PatternType = "Weekly",
                Description = $"Higher demand and prices typically seen on {string.Join(" and ", highDemandDays)}.",
                TimeWindows = highDemandDays,
                Confidence = 0.75m
            });
        }

        return patterns;
    }

    /// <summary>
    /// Generate time series forecast using ML.NET
    /// </summary>
    private ForecastResult GenerateForecast(List<IEXMarketData> data, int forecastDays, string marketType)
    {
        try
        {
            // Aggregate to daily data for forecasting
            var dailyData = data
                .GroupBy(d => d.Date.Date)
                .Select(g => new TimeSeriesData
                {
                    Date = g.Key,
                    Value = (float)g.Average(x => x.MCP)
                })
                .OrderBy(d => d.Date)
                .ToList();

            if (dailyData.Count < 30)
            {
                _logger.LogWarning("Insufficient data for forecasting. Need at least 30 days.");
                return new ForecastResult { MarketType = marketType, Metric = "MCP", ConfidenceScore = 0 };
            }

            // Create IDataView
            var dataView = _mlContext.Data.LoadFromEnumerable(dailyData);

            // Create forecasting pipeline
            var forecastingPipeline = _mlContext.Forecasting.ForecastBySsa(
                outputColumnName: "ForecastedValue",
                inputColumnName: nameof(TimeSeriesData.Value),
                windowSize: 7,
                seriesLength: Math.Min(dailyData.Count, 365),
                trainSize: dailyData.Count,
                horizon: forecastDays,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: "LowerBound",
                confidenceUpperBoundColumn: "UpperBound");

            // Train the model
            var model = forecastingPipeline.Fit(dataView);

            // Create forecast engine
            var forecastEngine = model.CreateTimeSeriesEngine<TimeSeriesData, TimeSeriesPrediction>(_mlContext);

            // Generate predictions
            var forecast = forecastEngine.Predict();

            var predictions = new List<ForecastPoint>();
            var lastDate = dailyData.Max(d => d.Date);

            for (int i = 0; i < forecastDays; i++)
            {
                predictions.Add(new ForecastPoint
                {
                    Date = lastDate.AddDays(i + 1),
                    PredictedValue = (decimal)forecast.ForecastedValue[i],
                    LowerBound = (decimal)forecast.LowerBound[i],
                    UpperBound = (decimal)forecast.UpperBound[i]
                });
            }

            return new ForecastResult
            {
                MarketType = marketType,
                Metric = "MCP",
                ForecastStartDate = lastDate.AddDays(1),
                Predictions = predictions,
                ConfidenceScore = 0.85m,
                ModelAccuracy = "SSA (Singular Spectrum Analysis)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating forecast");
            return new ForecastResult { MarketType = marketType, Metric = "MCP", ConfidenceScore = 0 };
        }
    }

    /// <summary>
    /// Generate business recommendations
    /// </summary>
    private List<BusinessRecommendation> GenerateRecommendations(MarketInsights insights, List<IEXMarketData> data)
    {
        var recommendations = new List<BusinessRecommendation>();

        // Analyze price trend for buy/sell recommendation
        var priceTrend = insights.Trends.FirstOrDefault(t => t.Type == "Price");
        if (priceTrend != null)
        {
            if (priceTrend.Direction == "Downward" && priceTrend.Strength > 50)
            {
                recommendations.Add(new BusinessRecommendation
                {
                    Action = "Buy",
                    MarketType = insights.MarketType,
                    Reasoning = "Prices are trending downward with strong momentum. Good opportunity to buy at lower prices.",
                    ConfidenceScore = priceTrend.ConfidenceScore,
                    SupportingFactors = new List<string>
                    {
                        $"Price decreased by {Math.Abs(insights.PriceAnalysis.PercentageChange):F2}%",
                        $"Current average (₹{insights.PriceAnalysis.CurrentAverage:F2}/kWh) below historical (₹{insights.PriceAnalysis.HistoricalAverage:F2}/kWh)",
                        $"Downward trend strength: {priceTrend.Strength:F0}%"
                    },
                    TimeHorizon = "Short-term",
                    ExpectedPriceRange = insights.Forecast?.Predictions.FirstOrDefault()?.PredictedValue
                });
            }
            else if (priceTrend.Direction == "Upward" && priceTrend.Strength > 50)
            {
                recommendations.Add(new BusinessRecommendation
                {
                    Action = "Sell",
                    MarketType = insights.MarketType,
                    Reasoning = "Prices are trending upward with strong momentum. Consider selling to maximize returns.",
                    ConfidenceScore = priceTrend.ConfidenceScore,
                    SupportingFactors = new List<string>
                    {
                        $"Price increased by {insights.PriceAnalysis.PercentageChange:F2}%",
                        $"Current average (₹{insights.PriceAnalysis.CurrentAverage:F2}/kWh) above historical (₹{insights.PriceAnalysis.HistoricalAverage:F2}/kWh)",
                        $"Upward trend strength: {priceTrend.Strength:F0}%"
                    },
                    TimeHorizon = "Short-term",
                    ExpectedPriceRange = insights.Forecast?.Predictions.FirstOrDefault()?.PredictedValue
                });
            }
            else
            {
                recommendations.Add(new BusinessRecommendation
                {
                    Action = "Hold",
                    MarketType = insights.MarketType,
                    Reasoning = "Market is relatively stable. Monitor for significant changes before taking action.",
                    ConfidenceScore = 0.7m,
                    SupportingFactors = new List<string>
                    {
                        $"Price volatility: {insights.PriceAnalysis.Volatility:F2}%",
                        $"Trend direction: {priceTrend.Direction}",
                        "No strong momentum detected"
                    },
                    TimeHorizon = "Medium-term"
                });
            }
        }

        // Volatility-based recommendation
        if (insights.PriceAnalysis.Volatility > 15)
        {
            recommendations.Add(new BusinessRecommendation
            {
                Action = "Caution",
                MarketType = insights.MarketType,
                Reasoning = "High market volatility detected. Consider risk management strategies.",
                ConfidenceScore = 0.9m,
                SupportingFactors = new List<string>
                {
                    $"Volatility at {insights.PriceAnalysis.Volatility:F2}%",
                    $"Standard deviation: ₹{insights.PriceAnalysis.StandardDeviation:F2}/kWh",
                    $"{insights.Anomalies.Count} anomalies detected"
                },
                TimeHorizon = "Immediate"
            });
        }

        // Time-of-day recommendation
        var dailyPattern = insights.Patterns.FirstOrDefault(p => p.PatternType == "Daily");
        if (dailyPattern != null)
        {
            recommendations.Add(new BusinessRecommendation
            {
                Action = "Optimize",
                MarketType = insights.MarketType,
                Reasoning = "Identified optimal trading windows based on daily patterns.",
                ConfidenceScore = dailyPattern.Confidence,
                SupportingFactors = new List<string>
                {
                    $"Best buying times: {insights.PriceAnalysis.LowestPriceTime.TimeBlock}",
                    $"Best selling times: {insights.PriceAnalysis.PeakPriceTime.TimeBlock}",
                    dailyPattern.Description
                },
                TimeHorizon = "Daily"
            });
        }

        return recommendations;
    }

    // Helper methods

    private List<IEXMarketData> GetFilteredData(InsightsRequest request)
    {
        var allData = _dataService.GetAllData();

        if (!string.IsNullOrEmpty(request.MarketType))
        {
            allData = allData.Where(d => d.Type.Equals(request.MarketType, StringComparison.OrdinalIgnoreCase));
        }

        if (request.StartDate.HasValue)
        {
            allData = allData.Where(d => d.Date >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            allData = allData.Where(d => d.Date <= request.EndDate.Value);
        }

        return allData.ToList();
    }

    private decimal CalculateStandardDeviation(List<decimal> values)
    {
        if (values.Count == 0) return 0;

        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow((double)(v - avg), 2));
        return (decimal)Math.Sqrt(sumOfSquares / values.Count);
    }

    private decimal CalculateVolatility(List<decimal> values)
    {
        if (values.Count < 2) return 0;

        var mean = values.Average();
        var stdDev = CalculateStandardDeviation(values);
        return mean > 0 ? (stdDev / mean) * 100 : 0;
    }

    private string DetermineTrend(List<decimal> values)
    {
        if (values.Count < 10) return "Insufficient Data";

        var firstHalf = values.Take(values.Count / 2).Average();
        var secondHalf = values.Skip(values.Count / 2).Average();

        var change = ((secondHalf - firstHalf) / firstHalf) * 100;

        return change > 5 ? "Increasing" :
               change < -5 ? "Decreasing" :
               "Stable";
    }

    private decimal CalculateTrendSlope(List<double> values)
    {
        if (values.Count < 2) return 0;

        var n = values.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumX2 += i * i;
        }

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return (decimal)slope;
    }

    private string GenerateTrendDescription(string type, decimal slope, decimal strength)
    {
        var direction = slope > 0 ? "increasing" : slope < 0 ? "decreasing" : "stable";
        var strengthDesc = strength > 75m ? "strong" :
                          strength > 50m ? "moderate" :
                          strength > 25m ? "weak" : "minimal";

        return $"{type} is {direction} with {strengthDesc} momentum.";
    }

    private decimal CalculateConfidence(int dataPoints, decimal strength)
    {
        var dataConfidence = Math.Min((decimal)dataPoints / 1000m, 0.5m);
        var strengthConfidence = strength / 200m;
        return Math.Min(dataConfidence + strengthConfidence, 1m);
    }

    /// <summary>
    /// Analyze cross-market comparisons for a specific period
    /// </summary>
    public List<CrossMarketComparison> AnalyzeCrossMarketComparisons(DateTime startDate, DateTime endDate)
    {
        var comparisons = new List<CrossMarketComparison>();

        // Get data for all three markets
        var allData = _dataService.GetDataByDateRange(startDate, endDate).ToList();

        var damData = allData.Where(d => d.Type == "DAM").ToList();
        var gdamData = allData.Where(d => d.Type == "GDAM").ToList();
        var rtmData = allData.Where(d => d.Type == "RTM").ToList();

        // Create lookup dictionaries
        var damLookup = damData.ToDictionary(d => (d.Date, d.TimeBlock), d => d.MCP);
        var gdamLookup = gdamData.ToDictionary(d => (d.Date, d.TimeBlock), d => d.MCP);
        var rtmLookup = rtmData.ToDictionary(d => (d.Date, d.TimeBlock), d => d.MCP);

        // Find all unique time slots
        var allSlots = damData.Select(d => (d.Date, d.TimeBlock))
            .Union(gdamData.Select(d => (d.Date, d.TimeBlock)))
            .Union(rtmData.Select(d => (d.Date, d.TimeBlock)))
            .Distinct()
            .ToList();

        var totalSlots = allSlots.Count;

        // GDAM > DAM
        var gdamGreaterDam = allSlots.Where(slot =>
            gdamLookup.ContainsKey(slot) && damLookup.ContainsKey(slot) &&
            gdamLookup[slot] > damLookup[slot]).ToList();

        comparisons.Add(new CrossMarketComparison
        {
            ComparisonType = "GDAM > DAM",
            TotalSlots = gdamGreaterDam.Count,
            Percentage = totalSlots > 0 ? (decimal)gdamGreaterDam.Count / totalSlots * 100 : 0,
            Slots = gdamGreaterDam.Take(10).Select(s => new TimeSlotComparison
            {
                Date = s.Date,
                TimeBlock = s.TimeBlock,
                Market1Value = gdamLookup[s],
                Market2Value = damLookup[s],
                Difference = gdamLookup[s] - damLookup[s]
            }).ToList()
        });

        // DAM > RTM
        var damGreaterRtm = allSlots.Where(slot =>
            damLookup.ContainsKey(slot) && rtmLookup.ContainsKey(slot) &&
            damLookup[slot] > rtmLookup[slot]).ToList();

        comparisons.Add(new CrossMarketComparison
        {
            ComparisonType = "DAM > RTM",
            TotalSlots = damGreaterRtm.Count,
            Percentage = totalSlots > 0 ? (decimal)damGreaterRtm.Count / totalSlots * 100 : 0,
            Slots = damGreaterRtm.Take(10).Select(s => new TimeSlotComparison
            {
                Date = s.Date,
                TimeBlock = s.TimeBlock,
                Market1Value = damLookup[s],
                Market2Value = rtmLookup[s],
                Difference = damLookup[s] - rtmLookup[s]
            }).ToList()
        });

        // RTM > GDAM
        var rtmGreaterGdam = allSlots.Where(slot =>
            rtmLookup.ContainsKey(slot) && gdamLookup.ContainsKey(slot) &&
            rtmLookup[slot] > gdamLookup[slot]).ToList();

        comparisons.Add(new CrossMarketComparison
        {
            ComparisonType = "RTM > GDAM",
            TotalSlots = rtmGreaterGdam.Count,
            Percentage = totalSlots > 0 ? (decimal)rtmGreaterGdam.Count / totalSlots * 100 : 0,
            Slots = rtmGreaterGdam.Take(10).Select(s => new TimeSlotComparison
            {
                Date = s.Date,
                TimeBlock = s.TimeBlock,
                Market1Value = rtmLookup[s],
                Market2Value = gdamLookup[s],
                Difference = rtmLookup[s] - gdamLookup[s]
            }).ToList()
        });

        // RTM > DAM
        var rtmGreaterDam = allSlots.Where(slot =>
            rtmLookup.ContainsKey(slot) && damLookup.ContainsKey(slot) &&
            rtmLookup[slot] > damLookup[slot]).ToList();

        comparisons.Add(new CrossMarketComparison
        {
            ComparisonType = "RTM > DAM",
            TotalSlots = rtmGreaterDam.Count,
            Percentage = totalSlots > 0 ? (decimal)rtmGreaterDam.Count / totalSlots * 100 : 0,
            Slots = rtmGreaterDam.Take(10).Select(s => new TimeSlotComparison
            {
                Date = s.Date,
                TimeBlock = s.TimeBlock,
                Market1Value = rtmLookup[s],
                Market2Value = damLookup[s],
                Difference = rtmLookup[s] - damLookup[s]
            }).ToList()
        });

        // DAM > GDAM
        var damGreaterGdam = allSlots.Where(slot =>
            damLookup.ContainsKey(slot) && gdamLookup.ContainsKey(slot) &&
            damLookup[slot] > gdamLookup[slot]).ToList();

        comparisons.Add(new CrossMarketComparison
        {
            ComparisonType = "DAM > GDAM",
            TotalSlots = damGreaterGdam.Count,
            Percentage = totalSlots > 0 ? (decimal)damGreaterGdam.Count / totalSlots * 100 : 0,
            Slots = damGreaterGdam.Take(10).Select(s => new TimeSlotComparison
            {
                Date = s.Date,
                TimeBlock = s.TimeBlock,
                Market1Value = damLookup[s],
                Market2Value = gdamLookup[s],
                Difference = damLookup[s] - gdamLookup[s]
            }).ToList()
        });

        return comparisons;
    }

    /// <summary>
    /// Analyze tariff range buckets for all markets
    /// </summary>
    public List<TariffRangeBucket> AnalyzeTariffRanges(string metric = "MCP", DateTime? startDate = null, DateTime? endDate = null)
    {
        var results = new List<TariffRangeBucket>();

        // Define ranges based on metric
        List<(decimal Min, decimal? Max, string Description)> ranges;

        if (metric.ToUpper() == "MCV")
        {
            // Volume ranges in GW
            ranges = new List<(decimal Min, decimal? Max, string Description)>
            {
                (0, 0.5m, "< 0.50 GW"),
                (0.5m, 1.0m, "0.50-0.99 GW"),
                (1.0m, 2.0m, "1.00-1.99 GW"),
                (2.0m, 3.0m, "2.00-2.99 GW"),
                (3.0m, 5.0m, "3.00-4.99 GW"),
                (5.0m, null, ">= 5.00 GW")
            };
        }
        else
        {
            // Price ranges in Rs./kWh
            ranges = new List<(decimal Min, decimal? Max, string Description)>
            {
                (0, 0.5m, "< Rs. 0.50/kWh"),
                (0.5m, 1.0m, "Rs. 0.50-0.99/kWh"),
                (1.0m, 3.0m, "Rs. 1.00-2.99/kWh"),
                (3.0m, 5.0m, "Rs. 3.00-4.99/kWh"),
                (5.0m, 7.0m, "Rs. 5.00-6.99/kWh"),
                (7.0m, 10.0m, "Rs. 7.00-9.99/kWh"),
                (10.0m, null, ">= Rs. 10.00/kWh")
            };
        }

        // Get all data based on date range if provided
        var allData = startDate.HasValue && endDate.HasValue
            ? _dataService.GetDataByDateRange(startDate.Value, endDate.Value).ToList()
            : _dataService.GetAllData().ToList();

        foreach (var market in new[] { "DAM", "GDAM", "RTM" })
        {
            var data = allData.Where(d => d.Type == market).ToList();
            var totalSlots = data.Count;

            foreach (var range in ranges)
            {
                int count;
                var value = metric.ToUpper() == "MCV"
                    ? (Func<IEXMarketData, decimal>)(d => d.MCV)
                    : (Func<IEXMarketData, decimal>)(d => d.MCP);

                if (range.Max.HasValue)
                {
                    count = data.Count(d => value(d) >= range.Min && value(d) < range.Max.Value);
                }
                else
                {
                    count = data.Count(d => value(d) >= range.Min);
                }

                results.Add(new TariffRangeBucket
                {
                    MarketType = market,
                    RangeDescription = range.Description,
                    MinValue = range.Min,
                    MaxValue = range.Max,
                    SlotCount = count,
                    Percentage = totalSlots > 0 ? (decimal)count / totalSlots * 100 : 0
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Analyze time slots to identify peak hours by market
    /// </summary>
    public List<TimeSlotPeakAnalysis> AnalyzeTimeSlotsByMarket(string metric = "MCP", DateTime? startDate = null, DateTime? endDate = null)
    {
        var results = new List<TimeSlotPeakAnalysis>();

        // Get all data based on date range if provided
        var allData = startDate.HasValue && endDate.HasValue
            ? _dataService.GetDataByDateRange(startDate.Value, endDate.Value).ToList()
            : _dataService.GetAllData().ToList();

        var isVolume = metric.ToUpper() == "MCV";

        foreach (var market in new[] { "DAM", "GDAM", "RTM" })
        {
            var data = allData.Where(d => d.Type == market).ToList();

            if (!data.Any())
                continue;

            // Group by time block
            var timeSlotStats = data.GroupBy(d => d.TimeBlock)
                .Select(g => new TimeSlotAverage
                {
                    TimeBlock = g.Key,
                    AverageMCP = isVolume ? g.Average(x => x.MCV) : g.Average(x => x.MCP),
                    MaxMCP = isVolume ? g.Max(x => x.MCV) : g.Max(x => x.MCP),
                    MinMCP = isVolume ? g.Min(x => x.MCV) : g.Min(x => x.MCP),
                    RecordCount = g.Count()
                })
                .OrderBy(x => x.TimeBlock)
                .ToList();

            // Identify peak hours (top 25%)
            var threshold = timeSlotStats.Count / 4;
            var peakHours = timeSlotStats
                .OrderByDescending(x => x.AverageMCP)
                .Take(threshold)
                .Select(x => x.TimeBlock)
                .OrderBy(x => x)
                .ToList();

            // Identify off-peak hours (bottom 25%)
            var offPeakHours = timeSlotStats
                .OrderBy(x => x.AverageMCP)
                .Take(threshold)
                .Select(x => x.TimeBlock)
                .OrderBy(x => x)
                .ToList();

            var analysis = new TimeSlotPeakAnalysis
            {
                MarketType = market,
                TimeSlots = timeSlotStats,
                PeakHours = peakHours,
                OffPeakHours = offPeakHours,
                Summary = $"{market} market has highest MCPs during {string.Join(", ", peakHours.Take(3))} and lowest during {string.Join(", ", offPeakHours.Take(3))}"
            };

            results.Add(analysis);
        }

        return results;
    }

    /// <summary>
    /// Generate forecasts for specific time slots (e.g., 5PM to 10PM)
    /// </summary>
    public List<TimeSlotForecastResult> GenerateTimeSlotForecasts(
        string marketType,
        string metric, // "MCP" or "MCV"
        List<string> timeSlots, // e.g., ["17:00", "18:00", "19:00", "20:00", "21:00", "22:00"]
        DateTime targetStartDate,
        DateTime targetEndDate,
        int historicalDaysToUse = 90)
    {
        var results = new List<TimeSlotForecastResult>();

        try
        {
            // Get all available data to determine actual date range
            var allAvailableData = _dataService.GetAllData()
                .Where(d => d.Type == marketType)
                .ToList();

            if (!allAvailableData.Any())
            {
                _logger.LogWarning($"No data found for market {marketType}");
                return results;
            }

            // Find the latest available date in the dataset
            var latestAvailableDate = allAvailableData.Max(d => d.Date);

            // Use historical data up to the latest available date
            var historicalEndDate = latestAvailableDate;
            var historicalStartDate = historicalEndDate.AddDays(-historicalDaysToUse);

            var historicalData = allAvailableData
                .Where(d => d.Date >= historicalStartDate && d.Date <= historicalEndDate)
                .ToList();

            if (!historicalData.Any())
            {
                _logger.LogWarning($"No historical data found for {marketType}");
                return results;
            }

            // Generate forecast for each time slot
            foreach (var timeSlot in timeSlots)
            {
                // Extract hour from time slot (e.g., "17:00" -> 17)
                int targetHour;
                if (timeSlot.Contains(":"))
                {
                    targetHour = int.Parse(timeSlot.Split(':')[0]);
                }
                else
                {
                    targetHour = int.Parse(timeSlot);
                }

                // Filter data for all 15-minute intervals within this hour
                // CSV has "17:00:00", "17:15:00", "17:30:00", "17:45:00" format
                var hourlyData = historicalData
                    .Where(d => !string.IsNullOrEmpty(d.TimeBlock) &&
                               d.TimeBlock.Split(':').Length > 0 &&
                               int.TryParse(d.TimeBlock.Split(':')[0], out int recordHour) &&
                               recordHour == targetHour)
                    .ToList();

                if (!hourlyData.Any())
                {
                    _logger.LogWarning($"No data found for hour {targetHour}:00");
                    continue;
                }

                // Aggregate by date - average all 15-min intervals within each hour for each day
                var dailyHourlyAverages = hourlyData
                    .GroupBy(d => d.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        AvgValue = metric == "MCV" ? g.Average(x => x.MCV) : g.Average(x => x.MCP)
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                if (dailyHourlyAverages.Count < 30)
                {
                    _logger.LogWarning($"Insufficient data for time slot {timeSlot}. Need at least 30 days, found {dailyHourlyAverages.Count} days.");
                    continue;
                }

                // Create time series data from daily hourly averages
                var timeSeriesData = dailyHourlyAverages.Select(d => new TimeSeriesData
                {
                    Date = d.Date,
                    Value = (float)d.AvgValue
                }).ToList();

                // Calculate average historical value
                var avgHistorical = (decimal)dailyHourlyAverages.Average(d => d.AvgValue);

                // Create IDataView
                var dataView = _mlContext.Data.LoadFromEnumerable(timeSeriesData);

                // Calculate forecast days
                int forecastDays = (targetEndDate - targetStartDate).Days;

                // Create forecasting pipeline
                var forecastingPipeline = _mlContext.Forecasting.ForecastBySsa(
                    outputColumnName: "ForecastedValue",
                    inputColumnName: nameof(TimeSeriesData.Value),
                    windowSize: 7,
                    seriesLength: Math.Min(timeSeriesData.Count, 90),
                    trainSize: timeSeriesData.Count,
                    horizon: forecastDays,
                    confidenceLevel: 0.90f,
                    confidenceLowerBoundColumn: "LowerBound",
                    confidenceUpperBoundColumn: "UpperBound");

                // Train the model
                var model = forecastingPipeline.Fit(dataView);

                // Create forecast engine
                var forecastEngine = model.CreateTimeSeriesEngine<TimeSeriesData, TimeSeriesPrediction>(_mlContext);

                // Generate predictions
                var forecast = forecastEngine.Predict();

                var predictions = new List<ForecastPoint>();

                for (int i = 0; i < forecastDays; i++)
                {
                    predictions.Add(new ForecastPoint
                    {
                        Date = targetStartDate.AddDays(i),
                        PredictedValue = (decimal)forecast.ForecastedValue[i],
                        LowerBound = (decimal)forecast.LowerBound[i],
                        UpperBound = (decimal)forecast.UpperBound[i]
                    });
                }

                results.Add(new TimeSlotForecastResult
                {
                    MarketType = marketType,
                    Metric = metric,
                    TimeBlock = timeSlot,
                    Predictions = predictions,
                    AverageHistorical = avgHistorical,
                    ConfidenceScore = 0.80m
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating time slot forecasts");
        }

        return results;
    }
}

/// <summary>
/// Time series data for ML.NET
/// </summary>
public class TimeSeriesData
{
    public DateTime Date { get; set; }
    public float Value { get; set; }
}

/// <summary>
/// Time series prediction
/// </summary>
public class TimeSeriesPrediction
{
    [VectorType(30)]
    public float[] ForecastedValue { get; set; } = Array.Empty<float>();

    [VectorType(30)]
    public float[] LowerBound { get; set; } = Array.Empty<float>();

    [VectorType(30)]
    public float[] UpperBound { get; set; } = Array.Empty<float>();
}

/// <summary>
/// Time slot specific forecast result
/// </summary>
public class TimeSlotForecastResult
{
    public string MarketType { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty; // "MCP" or "MCV"
    public string TimeBlock { get; set; } = string.Empty;
    public List<ForecastPoint> Predictions { get; set; } = new();
    public decimal AverageHistorical { get; set; }
    public decimal ConfidenceScore { get; set; }
}
