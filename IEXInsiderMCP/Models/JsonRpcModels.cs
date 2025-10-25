namespace IEXInsiderMCP.Models;

/// <summary>
/// JSON-RPC 2.0 Request
/// </summary>
public class JsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string? Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public object? Params { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 Response
/// </summary>
public class JsonRpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public string? Id { get; set; }
    public object? Result { get; set; }
    public JsonRpcError? Error { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 Error
/// </summary>
public class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

/// <summary>
/// JSON-RPC Error Codes
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
}

/// <summary>
/// Heat Map Request Model
/// </summary>
public class HeatMapRequest
{
    public string Query { get; set; } = string.Empty;
}
