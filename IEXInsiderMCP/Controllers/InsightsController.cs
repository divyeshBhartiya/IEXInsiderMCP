using IEXInsiderMCP.Models;
using IEXInsiderMCP.Services;
using Microsoft.AspNetCore.Mvc;

namespace IEXInsiderMCP.Controllers;

/// <summary>
/// AI-Powered Insights and Analytics Controller
/// </summary>
[ApiController]
[Route("api/insights")]
public class InsightsController : ControllerBase
{
    private readonly InsightsEngine _insightsEngine;
    private readonly NaturalLanguageEngine _nlEngine;
    private readonly ILogger<InsightsController> _logger;

    public InsightsController(
        InsightsEngine insightsEngine,
        NaturalLanguageEngine nlEngine,
        ILogger<InsightsController> logger)
    {
        _insightsEngine = insightsEngine;
        _nlEngine = nlEngine;
        _logger = logger;
    }

    /// <summary>
    /// Ask anything - Natural language query endpoint (Claude-like interaction)
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - "What insights can you provide about the DAM market?"
    /// - "Compare all three markets and tell me which one is best for buying"
    /// - "Forecast the next 30 days for RTM market"
    /// - "Should I buy or sell in GDAM market?"
    /// - "Show me any unusual patterns or anomalies"
    /// </remarks>
    [HttpPost("ask")]
    public ActionResult<IntelligentResponse> AskQuestion([FromBody] NaturalLanguageQuery query)
    {
        _logger.LogInformation("Natural language query received: {Query}", query.Question);

        try
        {
            var response = _nlEngine.ProcessQuery(query.Question);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing natural language query");
            return StatusCode(500, new
            {
                error = "An error occurred while processing your query",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get comprehensive market insights
    /// </summary>
    [HttpPost("generate")]
    public ActionResult<MarketInsights> GenerateInsights([FromBody] InsightsRequest request)
    {
        _logger.LogInformation("Generating insights for market: {MarketType}", request.MarketType ?? "ALL");

        try
        {
            var insights = _insightsEngine.GenerateInsights(request);
            return Ok(insights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating insights");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get time series forecast for a specific market
    /// </summary>
    [HttpGet("forecast")]
    public ActionResult<ForecastResult> GetForecast(
        [FromQuery] string marketType = "DAM",
        [FromQuery] int days = 30)
    {
        _logger.LogInformation("Generating {Days}-day forecast for {MarketType}", days, marketType);

        try
        {
            var request = new InsightsRequest
            {
                MarketType = marketType,
                IncludeForecasting = true,
                ForecastDays = days,
                IncludeRecommendations = false
            };

            var insights = _insightsEngine.GenerateInsights(request);

            if (insights.Forecast == null)
            {
                return BadRequest(new { error = "Insufficient data for forecasting. Need at least 30 days of historical data." });
            }

            return Ok(insights.Forecast);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating forecast");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get buy/sell recommendations for all markets
    /// </summary>
    [HttpGet("recommendations")]
    public ActionResult<List<BusinessRecommendation>> GetRecommendations([FromQuery] string? marketType = null)
    {
        _logger.LogInformation("Getting recommendations for market: {MarketType}", marketType ?? "ALL");

        try
        {
            var request = new InsightsRequest
            {
                MarketType = marketType,
                IncludeForecasting = true,
                ForecastDays = 30,
                IncludeRecommendations = true
            };

            var insights = _insightsEngine.GenerateInsights(request);

            return Ok(insights.Recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Compare all markets and get recommendations
    /// </summary>
    [HttpGet("compare")]
    public ActionResult<object> CompareMarkets()
    {
        _logger.LogInformation("Comparing all markets");

        try
        {
            var markets = new[] { "DAM", "GDAM", "RTM" };
            var allInsights = new Dictionary<string, MarketInsights>();

            foreach (var market in markets)
            {
                var request = new InsightsRequest
                {
                    MarketType = market,
                    IncludeForecasting = true,
                    ForecastDays = 30,
                    IncludeRecommendations = true
                };

                allInsights[market] = _insightsEngine.GenerateInsights(request);
            }

            // Determine best markets
            var bestForBuying = allInsights
                .OrderBy(kvp => kvp.Value.PriceAnalysis.CurrentAverage)
                .First();

            var bestForSelling = allInsights
                .OrderByDescending(kvp => kvp.Value.PriceAnalysis.CurrentAverage)
                .First();

            var mostStable = allInsights
                .OrderBy(kvp => kvp.Value.PriceAnalysis.Volatility)
                .First();

            return Ok(new
            {
                markets = allInsights,
                summary = new
                {
                    bestForBuying = new
                    {
                        market = bestForBuying.Key,
                        avgPrice = bestForBuying.Value.PriceAnalysis.CurrentAverage,
                        reason = $"Lowest average price at ₹{bestForBuying.Value.PriceAnalysis.CurrentAverage:F2}/kWh"
                    },
                    bestForSelling = new
                    {
                        market = bestForSelling.Key,
                        avgPrice = bestForSelling.Value.PriceAnalysis.CurrentAverage,
                        reason = $"Highest average price at ₹{bestForSelling.Value.PriceAnalysis.CurrentAverage:F2}/kWh"
                    },
                    mostStable = new
                    {
                        market = mostStable.Key,
                        volatility = mostStable.Value.PriceAnalysis.Volatility,
                        reason = $"Lowest volatility at {mostStable.Value.PriceAnalysis.Volatility:F2}%"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing markets");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Detect anomalies in market data
    /// </summary>
    [HttpGet("anomalies")]
    public ActionResult<List<AnomalyInsight>> GetAnomalies([FromQuery] string? marketType = null)
    {
        _logger.LogInformation("Detecting anomalies for market: {MarketType}", marketType ?? "ALL");

        try
        {
            var request = new InsightsRequest
            {
                MarketType = marketType,
                IncludeForecasting = false,
                IncludeRecommendations = false
            };

            var insights = _insightsEngine.GenerateInsights(request);

            return Ok(insights.Anomalies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting anomalies");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get identified patterns in market data
    /// </summary>
    [HttpGet("patterns")]
    public ActionResult<List<PatternInsight>> GetPatterns([FromQuery] string? marketType = null)
    {
        _logger.LogInformation("Identifying patterns for market: {MarketType}", marketType ?? "ALL");

        try
        {
            var request = new InsightsRequest
            {
                MarketType = marketType,
                IncludeForecasting = false,
                IncludeRecommendations = false
            };

            var insights = _insightsEngine.GenerateInsights(request);

            return Ok(insights.Patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying patterns");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Natural language query request
/// </summary>
public class NaturalLanguageQuery
{
    public string Question { get; set; } = string.Empty;
}
