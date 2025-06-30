using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReminderCompletionsController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<ReminderCompletionsController> _logger;

    public ReminderCompletionsController(MongoDbService mongoDbService, ILogger<ReminderCompletionsController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes reminder completions from the client to the database
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult> SyncReminderCompletions([FromBody] SyncReminderCompletionsRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Sync request received but no user ID found in claims");
                return Unauthorized();
            }

            if (request.Completions == null || !request.Completions.Any())
            {
                _logger.LogInformation("Sync request received with no completions for user {UserId}", userId);
                return Ok(new { message = "No completions to sync", synced = 0 });
            }

            var success = await _mongoDbService.SyncReminderCompletionsAsync(userId, request.Completions);
            
            if (success)
            {
                _logger.LogInformation("Successfully synced {CompletionCount} reminder completions for user {UserId}", 
                    request.Completions.Count, userId);
                
                return Ok(new { 
                    message = "Reminder completions synced successfully", 
                    synced = request.Completions.Count 
                });
            }

            return BadRequest(new { error = "Failed to sync reminder completions" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing reminder completions");
            return StatusCode(500, new { error = "Internal server error during sync" });
        }
    }

    /// <summary>
    /// Gets the user's reminder completions history
    /// </summary>
    [HttpGet("my-completions")]
    public async Task<ActionResult<List<ReminderCompletionDto>>> GetMyCompletions(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var completions = await _mongoDbService.GetUserReminderCompletionsAsync(userId, startDate, endDate);
            
            _logger.LogInformation("Retrieved {CompletionCount} reminder completions for user {UserId}", 
                completions.Count, userId);

            return Ok(completions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user reminder completions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the user's streak information
    /// </summary>
    [HttpGet("my-streak")]
    public async Task<ActionResult<UserStreakInfo>> GetMyStreak()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var streakInfo = await _mongoDbService.GetUserStreakInfoAsync(userId);
            
            _logger.LogInformation("Retrieved streak info for user {UserId}: Current={CurrentStreak}, Longest={LongestStreak}", 
                userId, streakInfo.CurrentStreak, streakInfo.LongestStreak);

            return Ok(streakInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user streak info");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the global leaderboard
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<ActionResult<List<LeaderboardEntry>>> GetLeaderboard([FromQuery] int limit = 50)
    {
        try
        {
            if (limit <= 0 || limit > 100)
            {
                limit = 50;
            }

            var leaderboard = await _mongoDbService.GetLeaderboardAsync(limit);
            
            _logger.LogInformation("Retrieved leaderboard with {EntryCount} entries", leaderboard.Count);

            return Ok(leaderboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaderboard");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the user's position in the leaderboard
    /// </summary>
    [HttpGet("my-rank")]
    public async Task<ActionResult> GetMyRank()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Get full leaderboard to find user's position
            var leaderboard = await _mongoDbService.GetLeaderboardAsync(1000); // Get more entries to find user
            var userEntry = leaderboard.FirstOrDefault(e => e.UserId == userId);

            if (userEntry == null)
            {
                return Ok(new { rank = 0, totalUsers = leaderboard.Count, message = "User not found in leaderboard" });
            }

            _logger.LogInformation("User {UserId} rank: {Rank}", userId, userEntry.Rank);

            return Ok(new { 
                rank = userEntry.Rank,
                totalUsers = leaderboard.Count,
                currentStreak = userEntry.CurrentStreak,
                longestStreak = userEntry.LongestStreak,
                totalCompletions = userEntry.TotalCompletions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rank");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Manual endpoint to record a single reminder completion (for testing or backup)
    /// </summary>
    [HttpPost("complete")]
    public async Task<ActionResult> CompleteReminder([FromBody] CreateReminderCompletionRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrEmpty(request.ReminderId) || string.IsNullOrEmpty(request.ReminderTitle))
            {
                return BadRequest(new { error = "ReminderId and ReminderTitle are required" });
            }

            var success = await _mongoDbService.SyncReminderCompletionsAsync(userId, new List<CreateReminderCompletionRequest> { request });
            
            if (success)
            {
                _logger.LogInformation("Recorded reminder completion for user {UserId}, reminder {ReminderId}", 
                    userId, request.ReminderId);
                
                return Ok(new { message = "Reminder completion recorded successfully" });
            }

            return BadRequest(new { error = "Failed to record reminder completion" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording reminder completion");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
} 