using IEXInsiderMCP.Models;
using Microsoft.AspNetCore.Mvc;

namespace IEXInsiderMCP.Controllers;

/// <summary>
/// Speech-to-Text API Controller using Web Speech API (browser-based)
/// For server-side processing, ML.NET can be extended with custom models
/// </summary>
[ApiController]
[Route("api/speech")]
public class SpeechController : ControllerBase
{
    private readonly ILogger<SpeechController> _logger;

    public SpeechController(ILogger<SpeechController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Convert speech to text - handled by browser Web Speech API
    /// </summary>
    [HttpPost("to-text")]
    public async Task<ActionResult<object>> ConvertSpeechToText([FromBody] SpeechToTextRequest request)
    {
        try
        {
            _logger.LogInformation("Speech-to-text request received");

            // Note: Speech recognition is best handled by browser's Web Speech API
            // Server-side speech processing with ML.NET would require training custom models
            // For now, we return instructions for client-side implementation

            await Task.CompletedTask;

            return Ok(new
            {
                success = true,
                text = request.AudioBase64,
                message = "Use browser Web Speech API for client-side speech recognition"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing speech-to-text");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Health check for speech service
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        return Ok(new
        {
            status = "ready",
            message = "Speech service uses browser Web Speech API",
            implementation = "Client-side speech recognition via JavaScript"
        });
    }
}
