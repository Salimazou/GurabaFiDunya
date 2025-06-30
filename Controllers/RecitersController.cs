using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;

namespace server.Controllers;

[ApiController]
[Route("api/reciters")]
public class RecitersController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<RecitersController> _logger;

    public RecitersController(
        MongoDbService mongoDbService,
        ILogger<RecitersController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    /// Get the most popular reciters based on favorites count
    /// </summary>
    [HttpGet("popular")]
    public async Task<IActionResult> GetMostPopularReciters([FromQuery] int limit = 10)
    {
        try
        {
            if (limit <= 0 || limit > 100)
            {
                return BadRequest(new { message = "Limit moet tussen 1 en 100 zijn" });
            }

            var popularReciters = await _mongoDbService.GetMostPopularRecitersAsync(limit);
            
            return Ok(new { 
                popularReciters,
                count = popularReciters.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular reciters");
            return StatusCode(500, new { message = "Fout bij ophalen populaire reciters" });
        }
    }

    /// <summary>
    /// Get analytics summary for reciters
    /// </summary>
    [HttpGet("analytics/summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAnalyticsSummary()
    {
        try
        {
            var popularReciters = await _mongoDbService.GetMostPopularRecitersAsync(5);
            
            return Ok(new { 
                topReciters = popularReciters,
                generatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analytics summary");
            return StatusCode(500, new { message = "Fout bij ophalen analytics" });
        }
    }
} 