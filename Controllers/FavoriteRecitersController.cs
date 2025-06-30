using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/users/me/favorite-reciters")]
[Authorize]
public class FavoriteRecitersController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<FavoriteRecitersController> _logger;

    public FavoriteRecitersController(
        MongoDbService mongoDbService,
        ILogger<FavoriteRecitersController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's favorite reciters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFavoriteReciters()
    {
        try
        {
            var userId = GetCurrentUserId();
            var favoriteReciters = await _mongoDbService.GetUserFavoriteRecitersAsync(userId);
            
            var result = favoriteReciters.Select(fr => new FavoriteReciterDto
            {
                ReciterId = fr.ReciterId,
                AddedAt = fr.AddedAt,
                Order = fr.Order,
                ListenCount = fr.ListenCount,
                LastListenedAt = fr.LastListenedAt
            }).ToList();
            
            return Ok(new { favoriteReciters = result, count = result.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite reciters for user {UserId}", GetCurrentUserId());
            return StatusCode(500, new { message = "Fout bij ophalen favoriete reciters" });
        }
    }

    /// <summary>
    /// Add a reciter to favorites
    /// </summary>
    [HttpPost("{reciterId}")]
    public async Task<IActionResult> AddFavoriteReciter(string reciterId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reciterId))
            {
                return BadRequest(new { message = "Reciter ID is verplicht" });
            }

            var userId = GetCurrentUserId();
            var success = await _mongoDbService.AddFavoriteReciterAsync(userId, reciterId);

            if (!success)
            {
                return Conflict(new { message = "Reciter staat al in favorieten" });
            }

            _logger.LogInformation("User {UserId} added reciter {ReciterId} to favorites", userId, reciterId);
            return Ok(new { message = "Reciter toegevoegd aan favorieten", reciterId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding favorite reciter {ReciterId} for user {UserId}", reciterId, GetCurrentUserId());
            return StatusCode(500, new { message = "Fout bij toevoegen favoriet" });
        }
    }

    /// <summary>
    /// Remove a reciter from favorites
    /// </summary>
    [HttpDelete("{reciterId}")]
    public async Task<IActionResult> RemoveFavoriteReciter(string reciterId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reciterId))
            {
                return BadRequest(new { message = "Reciter ID is verplicht" });
            }

            var userId = GetCurrentUserId();
            var success = await _mongoDbService.RemoveFavoriteReciterAsync(userId, reciterId);

            if (!success)
            {
                return NotFound(new { message = "Reciter niet gevonden in favorieten" });
            }

            _logger.LogInformation("User {UserId} removed reciter {ReciterId} from favorites", userId, reciterId);
            return Ok(new { message = "Reciter verwijderd uit favorieten", reciterId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite reciter {ReciterId} for user {UserId}", reciterId, GetCurrentUserId());
            return StatusCode(500, new { message = "Fout bij verwijderen favoriet" });
        }
    }

    /// <summary>
    /// Check if a reciter is in favorites
    /// </summary>
    [HttpGet("{reciterId}/status")]
    public async Task<IActionResult> GetFavoriteStatus(string reciterId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reciterId))
            {
                return BadRequest(new { message = "Reciter ID is verplicht" });
            }

            var userId = GetCurrentUserId();
            var isFavorite = await _mongoDbService.IsFavoriteReciterAsync(userId, reciterId);

            return Ok(new { isFavorite, reciterId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking favorite status for reciter {ReciterId}", reciterId);
            return StatusCode(500, new { message = "Fout bij controleren favoriet status" });
        }
    }

    /// <summary>
    /// Reorder favorite reciters
    /// </summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderFavoriteReciters([FromBody] ReorderFavoriteRecitersRequest request)
    {
        try
        {
            if (request?.ReciterIds == null || !request.ReciterIds.Any())
            {
                return BadRequest(new { message = "Reciter IDs lijst is verplicht" });
            }

            var userId = GetCurrentUserId();
            var success = await _mongoDbService.ReorderFavoriteRecitersAsync(userId, request.ReciterIds);

            if (!success)
            {
                return BadRequest(new { message = "Herordenen mislukt - controleer of alle reciters in favorieten staan" });
            }

            return Ok(new { message = "Volgorde succesvol bijgewerkt", count = request.ReciterIds.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering favorite reciters for user {UserId}", GetCurrentUserId());
            return StatusCode(500, new { message = "Fout bij herordenen favorieten" });
        }
    }

    /// <summary>
    /// Increment listen count for a favorite reciter
    /// </summary>
    [HttpPost("{reciterId}/listen")]
    public async Task<IActionResult> IncrementListenCount(string reciterId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reciterId))
            {
                return BadRequest(new { message = "Reciter ID is verplicht" });
            }

            var userId = GetCurrentUserId();
            await _mongoDbService.IncrementListenCountAsync(userId, reciterId);

            return Ok(new { message = "Luister telling bijgewerkt", reciterId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing listen count for reciter {ReciterId}", reciterId);
            return StatusCode(500, new { message = "Fout bij bijwerken luister telling" });
        }
    }

    /// <summary>
    /// Get user's favorite reciters count
    /// </summary>
    [HttpGet("count")]
    public async Task<IActionResult> GetFavoriteRecitersCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _mongoDbService.GetUserFavoriteRecitersCountAsync(userId);

            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite reciters count for user {UserId}", GetCurrentUserId());
            return StatusCode(500, new { message = "Fout bij ophalen aantal favorieten" });
        }
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Gebruiker niet geauthenticeerd");
        }
        return userId;
    }
} 