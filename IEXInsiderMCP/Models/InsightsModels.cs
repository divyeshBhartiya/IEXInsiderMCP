namespace IEXInsiderMCP.Models;

/// <summary>
/// Represents comprehensive market insights
/// </summary>
public class MarketInsights
{
    public string MarketType { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public int DataPointsAnalyzed { get; set; }

    // Price Insights
    public PriceInsights PriceAnalysis { get; set; } = new();

    // Volume Insights
    public VolumeInsights VolumeAnalysis { get; set; } = new();

    // Trends
    public List<TrendInsight> Trends { get; set; } = new();

    // Anomalies
    public List<AnomalyInsight> Anomalies { get; set; } = new();

    // Patterns
    public List<PatternInsight> Patterns { get; set; } = new();

    // Forecasts
    public ForecastResult? Forecast { get; set; }

    // Business Recommendations
    public List<BusinessRecommendation> Recommendations { get; set; } = new();
}

/// <summary>
/// Price-specific insights
/// </summary>
public class PriceInsights
{
    public decimal CurrentAverage { get; set; }
    public decimal HistoricalAverage { get; set; }
    public decimal PercentageChange { get; set; }
    public decimal Volatility { get; set; }
    public decimal StandardDeviation { get; set; }
    public TimeSlot PeakPriceTime { get; set; } = new();
    public TimeSlot LowestPriceTime { get; set; } = new();
    public string Trend { get; set; } = string.Empty; // "Increasing", "Decreasing", "Stable"
}

/// <summary>
/// Volume-specific insights
/// </summary>
public class VolumeInsights
{
    public decimal CurrentAverage { get; set; }
    public decimal HistoricalAverage { get; set; }
    public decimal PercentageChange { get; set; }
    public decimal PeakVolume { get; set; }
    public TimeSlot PeakVolumeTime { get; set; } = new();
    public string Trend { get; set; } = string.Empty;
}

/// <summary>
/// Time slot information
/// </summary>
public class TimeSlot
{
    public DateTime Date { get; set; }
    public string TimeBlock { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

/// <summary>
/// Trend insight
/// </summary>
public class TrendInsight
{
    public string Type { get; set; } = string.Empty; // "Price", "Volume", "Demand"
    public string Direction { get; set; } = string.Empty; // "Upward", "Downward", "Sideways"
    public decimal Strength { get; set; } // 0-100
    public string Description { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; } // 0-1
}

/// <summary>
/// Anomaly detection result
/// </summary>
public class AnomalyInsight
{
    public DateTime Date { get; set; }
    public string TimeBlock { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "PriceSpike", "VolumeDrop", etc.
    public decimal ExpectedValue { get; set; }
    public decimal ActualValue { get; set; }
    public decimal Deviation { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "Low", "Medium", "High"
}

/// <summary>
/// Pattern insight
/// </summary>
public class PatternInsight
{
    public string PatternType { get; set; } = string.Empty; // "Seasonal", "Daily", "Weekly"
    public string Description { get; set; } = string.Empty;
    public List<string> TimeWindows { get; set; } = new();
    public decimal Confidence { get; set; }
}

/// <summary>
/// Forecast result
/// </summary>
public class ForecastResult
{
    public string MarketType { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty; // "MCP" or "MCV"
    public DateTime ForecastStartDate { get; set; }
    public List<ForecastPoint> Predictions { get; set; } = new();
    public decimal ConfidenceScore { get; set; }
    public string ModelAccuracy { get; set; } = string.Empty;
}

/// <summary>
/// Individual forecast point
/// </summary>
public class ForecastPoint
{
    public DateTime Date { get; set; }
    public decimal PredictedValue { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
}

/// <summary>
/// Business recommendation
/// </summary>
public class BusinessRecommendation
{
    public string Action { get; set; } = string.Empty; // "Buy", "Sell", "Hold"
    public string MarketType { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; } // 0-1
    public List<string> SupportingFactors { get; set; } = new();
    public string TimeHorizon { get; set; } = string.Empty; // "Immediate", "Short-term", "Long-term"
    public decimal? ExpectedPriceRange { get; set; }
}

/// <summary>
/// Comparative market analysis
/// </summary>
public class MarketComparison
{
    public DateTime AnalysisDate { get; set; }
    public List<MarketMetrics> Markets { get; set; } = new();
    public string BestMarketForBuying { get; set; } = string.Empty;
    public string BestMarketForSelling { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Metrics for a specific market
/// </summary>
public class MarketMetrics
{
    public string MarketType { get; set; } = string.Empty;
    public decimal AveragePrice { get; set; }
    public decimal PriceVolatility { get; set; }
    public decimal AverageVolume { get; set; }
    public decimal LiquidityScore { get; set; } // 0-100
    public decimal RiskScore { get; set; } // 0-100
    public decimal OpportunityScore { get; set; } // 0-100
}

/// <summary>
/// Request for insights generation
/// </summary>
public class InsightsRequest
{
    public string? MarketType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IncludeForecasting { get; set; } = true;
    public int ForecastDays { get; set; } = 30;
    public bool IncludeRecommendations { get; set; } = true;
}

/// <summary>
/// Natural language query response
/// </summary>
public class IntelligentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<string> KeyFindings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
    public List<ChartData> Charts { get; set; } = new();
}

/// <summary>
/// Chart data for visualization
/// </summary>
public class ChartData
{
    public string ChartType { get; set; } = string.Empty; // "Line", "Bar", "Forecast", "Scatter", "combined_bar_line"
    public string Title { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public List<ChartDataset> Datasets { get; set; } = new();
    public bool Has_Dual_Axes { get; set; } // For combined bar+line charts with dual Y-axes
}

/// <summary>
/// Dataset for charts
/// </summary>
public class ChartDataset
{
    public string Label { get; set; } = string.Empty;
    public List<decimal> Data { get; set; } = new();
    public string Color { get; set; } = string.Empty;
    public string BorderColor { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public bool Fill { get; set; }
    public decimal? Tension { get; set; }
    public string? Type { get; set; } // "bar" or "line" for mixed charts
    public string? YAxisID { get; set; } // For dual Y-axis charts: "y-mcp" or "y-mcv"
    public int? BorderWidth { get; set; }
    public int? Order { get; set; } // Drawing order (lower = drawn first/on bottom)
}

/// <summary>
/// Cross-market comparison result
/// </summary>
public class CrossMarketComparison
{
    public string ComparisonType { get; set; } = string.Empty; // "GDAM>DAM", "DAM>RTM", etc.
    public int TotalSlots { get; set; }
    public decimal Percentage { get; set; }
    public List<TimeSlotComparison> Slots { get; set; } = new();
}

/// <summary>
/// Individual time slot comparison
/// </summary>
public class TimeSlotComparison
{
    public DateTime Date { get; set; }
    public string TimeBlock { get; set; } = string.Empty;
    public decimal Market1Value { get; set; }
    public decimal Market2Value { get; set; }
    public decimal Difference { get; set; }
}

/// <summary>
/// Tariff range bucket analysis
/// </summary>
public class TariffRangeBucket
{
    public string MarketType { get; set; } = string.Empty;
    public string RangeDescription { get; set; } = string.Empty;
    public decimal MinValue { get; set; }
    public decimal? MaxValue { get; set; } // null for ">= 10.00"
    public int SlotCount { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Time slot peak analysis by market
/// </summary>
public class TimeSlotPeakAnalysis
{
    public string MarketType { get; set; } = string.Empty;
    public List<TimeSlotAverage> TimeSlots { get; set; } = new();
    public List<string> PeakHours { get; set; } = new();
    public List<string> OffPeakHours { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Average values for a specific time slot
/// </summary>
public class TimeSlotAverage
{
    public string TimeBlock { get; set; } = string.Empty;
    public decimal AverageMCP { get; set; }
    public decimal MaxMCP { get; set; }
    public decimal MinMCP { get; set; }
    public int RecordCount { get; set; }
}
