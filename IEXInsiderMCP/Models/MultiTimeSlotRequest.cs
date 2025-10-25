namespace IEXInsiderMCP.Models;

/// <summary>
/// Market types enum
/// </summary>
public enum MarketType
{
    DAM,
    GDAM,
    RTM
}

/// <summary>
/// Request model for multi-time-slot analysis
/// </summary>
public class MultiTimeSlotRequest
{
    public string Query { get; set; } = string.Empty;
    public List<TimeSlotDefinition> TimeSlots { get; set; } = new();
    public List<MarketType> Markets { get; set; } = new();
    public string? Month { get; set; } // Optional: "September", "November", etc.
    public int? Year { get; set; } // Optional: 2024, 2025
    public string MetricType { get; set; } = "mcp"; // mcp or mcv (for backwards compatibility)
    public string ChartType { get; set; } = "heatmap"; // heatmap, bar, line (for backwards compatibility)
    public List<string> MetricTypes { get; set; } = new(); // Support multiple metrics: mcp, mcv
    public Dictionary<string, string> MetricChartTypes { get; set; } = new(); // Metric -> ChartType mapping
}

/// <summary>
/// Time slot definition
/// </summary>
public class TimeSlotDefinition
{
    public string Name { get; set; } = string.Empty; // e.g., "Morning", "Evening"
    public string StartTime { get; set; } = string.Empty; // e.g., "09:00"
    public string EndTime { get; set; } = string.Empty; // e.g., "17:00"
}

/// <summary>
/// Multi-time-slot response
/// </summary>
public class MultiTimeSlotResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<TimeSlotResult> Results { get; set; } = new();
}

/// <summary>
/// Result for a single time slot
/// </summary>
public class TimeSlotResult
{
    public string TimeSlotName { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public Dictionary<string, object> ChartData { get; set; } = new();
    public Dictionary<string, decimal> Statistics { get; set; } = new();
}
