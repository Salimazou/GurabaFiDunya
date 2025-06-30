using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class SimpleFavoriteRecitersController : ControllerBase
{
    private readonly SimpleDbService _db;
    private readonly ILogger<SimpleFavoriteRecitersController> _logger;

    public SimpleFavoriteRecitersController(SimpleDbService db, ILogger<SimpleFavoriteRecitersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> GetMyFavorites()
    {
        try
        {
            var userId = GetCurrentUserId();
            var favorites = await _db.GetUserFavoriteRecitersAsync(userId);
            return Ok(favorites);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite reciters");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpPost("{reciterId}")]
    public async Task<IActionResult> AddFavorite(string reciterId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var wasAdded = await _db.AddFavoriteReciterAsync(userId, reciterId);
            
            if (wasAdded)
            {
                return Ok(new { message = "Reciter added to favorites", success = true });
            }
            else
            {
                return Ok(new { message = "Reciter already in favorites", success = false });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding favorite reciter");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpDelete("{reciterId}")]
    public async Task<IActionResult> RemoveFavorite(string reciterId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _db.RemoveFavoriteReciterAsync(userId, reciterId);
            return Ok(new { message = "Reciter removed from favorites" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite reciter");
            return StatusCode(500, new { message = "Server error" });
        }
    }
} 