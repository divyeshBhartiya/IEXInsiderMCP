using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IEXInsiderMCP.Services;

/// <summary>
/// AI Service to integrate with Claude API or OpenAI API for conversational responses
/// </summary>
public class AIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _provider;
    private readonly string _apiKey;
    private readonly string _model;

    public AIService(HttpClient httpClient, ILogger<AIService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // Read configuration
        _provider = _configuration["AI:Provider"] ?? "Claude"; // Claude or OpenAI
        _apiKey = _configuration["AI:ApiKey"] ?? "";
        _model = _configuration["AI:Model"] ?? (_provider == "Claude" ? "claude-sonnet-4-20250514" : "gpt-4o");

        _logger.LogInformation("AI Service initialized with provider: {Provider}, model: {Model}", _provider, _model);
    }

    /// <summary>
    /// Generate conversational insights from IEX market data
    /// </summary>
    public async Task<string> GenerateInsights(string userQuery, string dataContext, List<string>? conversationHistory = null)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("AI API key not configured. Returning fallback response.");
            return GenerateFallbackResponse(userQuery);
        }

        try
        {
            _logger.LogInformation("Generating AI insights for query: {Query}", userQuery);

            if (_provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
            {
                return await CallClaudeAPI(userQuery, dataContext, conversationHistory);
            }
            else if (_provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                return await CallOpenAIAPI(userQuery, dataContext, conversationHistory);
            }
            else
            {
                _logger.LogError("Unknown AI provider: {Provider}", _provider);
                return GenerateFallbackResponse(userQuery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI insights");
            return GenerateFallbackResponse(userQuery);
        }
    }

    private async Task<string> CallClaudeAPI(string userQuery, string dataContext, List<string>? conversationHistory)
    {
        var systemPrompt = @"You are an expert market analyst specializing in Indian Energy Exchange (IEX) electricity markets. You have access to comprehensive IEX market data including Day-Ahead Market (DAM), Green Day-Ahead Market (GDAM), and Real-Time Market (RTM).

Your role is to:
1. Analyze the provided market data and answer user questions conversationally
2. Provide insights, trends, comparisons, and recommendations
3. Use tables, bullet points, and clear formatting in your responses
4. Include relevant statistics (averages, peaks, trends)
5. Add context about market dynamics (e.g., green premium, RTM volatility)
6. Format prices in ₹/kWh and volumes in GW
7. Be concise but comprehensive

The data provided includes:
- Market Clearing Price (MCP) in Rs/kWh
- Market Clearing Volume (MCV) in GW
- Market types: DAM, GDAM, RTM
- Time periods: 2023-2025
- 15-minute time blocks (96 per day)";

        var messages = new List<ClaudeMessage>();

        // Add conversation history if available
        if (conversationHistory != null && conversationHistory.Any())
        {
            foreach (var historyItem in conversationHistory.TakeLast(10)) // Last 10 messages for context
            {
                messages.Add(new ClaudeMessage
                {
                    Role = "user",
                    Content = historyItem
                });
            }
        }

        // Add current query with data context
        var userMessage = $@"User Query: {userQuery}

Market Data Analysis:
{dataContext}

Please analyze this data and provide a conversational, insightful response to the user's query. Include key findings, comparisons, and context where relevant.";

        messages.Add(new ClaudeMessage
        {
            Role = "user",
            Content = userMessage
        });

        var requestBody = new
        {
            model = _model,
            max_tokens = 2048,
            system = systemPrompt,
            messages = messages
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
            return GenerateFallbackResponse(userQuery);
        }

        var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return claudeResponse?.Content?.FirstOrDefault()?.Text ?? GenerateFallbackResponse(userQuery);
    }

    private async Task<string> CallOpenAIAPI(string userQuery, string dataContext, List<string>? conversationHistory)
    {
        var systemPrompt = @"You are an expert market analyst specializing in Indian Energy Exchange (IEX) electricity markets. You have access to comprehensive IEX market data including Day-Ahead Market (DAM), Green Day-Ahead Market (GDAM), and Real-Time Market (RTM).

Your role is to:
1. Analyze the provided market data and answer user questions conversationally
2. Provide insights, trends, comparisons, and recommendations
3. Use tables, bullet points, and clear formatting in your responses
4. Include relevant statistics (averages, peaks, trends)
5. Add context about market dynamics (e.g., green premium, RTM volatility)
6. Format prices in ₹/kWh and volumes in GW
7. Be concise but comprehensive";

        var messages = new List<OpenAIMessage>
        {
            new OpenAIMessage { Role = "system", Content = systemPrompt }
        };

        // Add conversation history
        if (conversationHistory != null && conversationHistory.Any())
        {
            foreach (var historyItem in conversationHistory.TakeLast(10))
            {
                messages.Add(new OpenAIMessage { Role = "user", Content = historyItem });
            }
        }

        // Add current query
        var userMessage = $@"User Query: {userQuery}

Market Data Analysis:
{dataContext}

Please analyze this data and provide a conversational, insightful response to the user's query.";

        messages.Add(new OpenAIMessage { Role = "user", Content = userMessage });

        var requestBody = new
        {
            model = _model,
            messages = messages,
            max_tokens = 2048,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(requestBody);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
            return GenerateFallbackResponse(userQuery);
        }

        var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? GenerateFallbackResponse(userQuery);
    }

    private string GenerateFallbackResponse(string query)
    {
        return $@"## Query Analysis

I received your query: ""{query}""

However, AI API integration is not configured. To enable conversational AI responses:

1. Add your API key to appsettings.json:
   - For Claude API: Add `""AI:ApiKey""` with your Anthropic API key
   - For OpenAI: Add `""AI:ApiKey""` with your OpenAI API key

2. Set the provider: `""AI:Provider"": ""Claude""` or `""OpenAI""`

The data and charts are still available, but conversational insights require API configuration.";
    }

    // Response models for Claude API
    private class ClaudeMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class ClaudeResponse
    {
        [JsonPropertyName("content")]
        public List<ClaudeContent>? Content { get; set; }
    }

    private class ClaudeContent
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    // Response models for OpenAI API
    private class OpenAIMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }
    }

    private class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }
}
