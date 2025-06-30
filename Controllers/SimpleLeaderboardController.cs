using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/leaderboard")]
[Authorize]
public class SimpleLeaderboardController : ControllerBase
{
    private readonly SimpleDbService _db;
    private readonly ILogger<SimpleLeaderboardController> _logger;
    private readonly IConfiguration _config;

    public SimpleLeaderboardController(SimpleDbService db, ILogger<SimpleLeaderboardController> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    private string GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    private async Task<bool> IsCurrentUserAdminAsync()
    {
        var currentUserId = GetCurrentUserId();
        var currentUser = await _db.GetUserByIdAsync(currentUserId);
        
        if (currentUser == null) return false;
        
        // Check multiple admin emails from configuration
        var adminEmails = _config.GetSection("AdminEmails").Get<string[]>() ?? new[] { "salim@example.com" };
        
        return adminEmails.Contains(currentUser.Email, StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 10)
    {
        try
        {
            var leaderboard = await _db.GetLeaderboardAsync(limit);
            return Ok(leaderboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaderboard");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpGet("my-streak")]
    public async Task<IActionResult> GetMyStreak()
    {
        try
        {
            var userId = GetCurrentUserId();
            var streak = await _db.GetUserStreakAsync(userId);
            
            if (streak == null)
            {
                streak = new UserStreak { UserId = userId };
            }
            
            return Ok(streak);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user streak");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    // ADMIN ENDPOINTS - Now with proper security
    [HttpGet("admin/logs")]
    public async Task<IActionResult> GetReminderLogs(
        [FromQuery] string? userId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // Secure admin check using configuration
            if (!await IsCurrentUserAdminAsync())
            {
                return Forbid("Admin access required");
            }

            var logs = await _db.GetReminderLogsAsync(userId, startDate, endDate);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder logs");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpGet("admin/users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            // Secure admin check using configuration
            if (!await IsCurrentUserAdminAsync())
            {
                return Forbid("Admin access required");
            }

            // Get basic user info with their streaks
            var leaderboard = await _db.GetLeaderboardAsync(100); // Get all users
            return Ok(leaderboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { message = "Server error" });
        }
    }
} 