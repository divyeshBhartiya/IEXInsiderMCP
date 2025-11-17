namespace IEXInsiderMCP.Models;

/// <summary>
/// Represents Indian Energy Exchange market data record
/// </summary>
public class IEXMarketData
{
    /// <summary>
    /// Market type: DAM (Day-Ahead Market), GDAM (Green Day-Ahead Market), RTM (Real-Time Market)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Year of the transaction
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Date of the transaction
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Time block (15-minute intervals, 96 blocks per day)
    /// </summary>
    public string TimeBlock { get; set; } = string.Empty;

    /// <summary>
    /// IEX Demand in GigaWatts
    /// </summary>
    public decimal IEXDemand { get; set; }

    /// <summary>
    /// IEX Supply in GigaWatts
    /// </summary>
    public decimal IEXSupply { get; set; }

    /// <summary>
    /// Market Clearing Price in Rs./kWh
    /// </summary>
    public decimal MCP { get; set; }

    /// <summary>
    /// Market Clearing Volume in GigaWatts
    /// </summary>
    public decimal MCV { get; set; }
}

/// <summary>
/// Represents the result of a query operation
/// </summary>
public class QueryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<IEXMarketData>? Data { get; set; }
    public Dictionary<string, object>? Aggregations { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public int TotalRecords { get; set; }
    public string? AIInsights { get; set; } // AI-generated conversational insights
}

/// <summary>
/// Represents a natural language query request
/// </summary>
public class NLQueryRequest
{
    public string Query { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

/// <summary>
/// Universal query request with support for filters and aggregations
/// </summary>
public class UniversalQueryRequest
{
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, object>? Filters { get; set; }
    public string? Aggregation { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("group_by")]
    public string? GroupBy { get; set; }

    public int? Limit { get; set; }
}

/// <summary>
/// Represents a speech-to-text request
/// </summary>
public class SpeechToTextRequest
{
    public string AudioBase64 { get; set; } = string.Empty;
    public string? Format { get; set; } // wav, mp3, ogg
}

/// <summary>
/// MCP Tool definition
/// </summary>
public class MCPTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object>? InputSchema { get; set; }
}

/// <summary>
/// MCP Tool call request
/// </summary>
public class MCPToolCallRequest
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// MCP Tool call response
/// </summary>
public class MCPToolCallResponse
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}
