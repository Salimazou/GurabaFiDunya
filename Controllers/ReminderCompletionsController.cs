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
                return Unauthorized();
            }

            if (request?.Completions == null)
            {
                return BadRequest(new { error = "Completions array is required" });
            }

            _logger.LogInformation("Syncing {CompletionCount} reminder completions for user {UserId}", 
                request.Completions.Count, userId);

            // Log each completion for debugging
            foreach (var completion in request.Completions)
            {
                _logger.LogInformation("Completion: ReminderId={ReminderId}, Title={ReminderTitle}, Date={CompletionDate}", 
                    completion.ReminderId, completion.ReminderTitle, completion.CompletionDate);
            }

            var success = await _mongoDbService.SyncReminderCompletionsAsync(userId, request.Completions);
            
            if (success)
            {
                return Ok(new { message = "Sync completed successfully", syncedCount = request.Completions.Count });
            }

            return BadRequest(new { error = "Sync failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync");
            return StatusCode(500, new { error = "Internal server error during sync", details = ex.Message });
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
                _logger.LogWarning("GetMyStreak request received but no user ID found in claims");
                return Unauthorized(new { error = "User not authenticated" });
            }

            var streakInfo = await _mongoDbService.GetUserStreakInfoAsync(userId);
            
            _logger.LogInformation("Retrieved streak info for user {UserId}: Current={CurrentStreak}, Longest={LongestStreak}", 
                userId, streakInfo.CurrentStreak, streakInfo.LongestStreak);

            return Ok(streakInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user streak info");
            return StatusCode(500, new { error = "Internal server error while getting streak info" });
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
            return StatusCode(500, new { error = "Internal server error while getting leaderboard" });
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
                _logger.LogWarning("GetMyRank request received but no user ID found in claims");
                return Unauthorized(new { error = "User not authenticated" });
            }

            // Get full leaderboard to find user's position
            var leaderboard = await _mongoDbService.GetLeaderboardAsync(1000); // Get more entries to find user
            var userEntry = leaderboard.FirstOrDefault(e => e.UserId == userId);

            if (userEntry == null)
            {
                _logger.LogInformation("User {UserId} not found in leaderboard", userId);
                return Ok(new { 
                    rank = 0, 
                    totalUsers = leaderboard.Count, 
                    currentStreak = 0,
                    longestStreak = 0,
                    totalCompletions = 0,
                    message = "User not found in leaderboard" 
                });
            }

            _logger.LogInformation("User {UserId} rank: {Rank}/{TotalUsers}", userId, userEntry.Rank, leaderboard.Count);

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
            return StatusCode(500, new { error = "Internal server error while getting user rank" });
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

    /// <summary>
    /// Development endpoint to create test data (remove in production)
    /// </summary>
    [HttpPost("create-test-data")]
    public async Task<ActionResult> CreateTestData()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            // Create some test reminder completions for the past week
            var testCompletions = new List<CreateReminderCompletionRequest>();
            var today = DateTime.UtcNow.Date;

            for (int i = 0; i < 7; i++)
            {
                var completionDate = today.AddDays(-i);
                testCompletions.Add(new CreateReminderCompletionRequest
                {
                    ReminderId = Guid.NewGuid().ToString(),
                    ReminderTitle = $"Test Reminder {i + 1}",
                    CompletedAt = completionDate.AddHours(10),
                    CompletionDate = completionDate,
                    DeviceId = "test-device"
                });
            }

            var success = await _mongoDbService.SyncReminderCompletionsAsync(userId, testCompletions);
            
            if (success)
            {
                _logger.LogInformation("Created {Count} test reminder completions for user {UserId}", 
                    testCompletions.Count, userId);
                
                return Ok(new { 
                    message = "Test data created successfully", 
                    completions = testCompletions.Count 
                });
            }

            return BadRequest(new { error = "Failed to create test data" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test data");
            return StatusCode(500, new { error = "Internal server error while creating test data" });
        }
    }

    /// <summary>
    /// Debug endpoint to test database connection
    /// </summary>
    [HttpGet("debug/db-test")]
    public async Task<ActionResult> TestDatabase()
    {
        try
        {
            var canPing = await _mongoDbService.PingAsync();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            return Ok(new { 
                databaseConnected = canPing,
                userId = userId,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database test failed");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Debug endpoint to test direct reminder completion insert
    /// </summary>
    [HttpPost("debug/simple-insert")]
    public async Task<ActionResult> TestSimpleInsert()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Try a very simple single completion
            var completion = new CreateReminderCompletionRequest
            {
                ReminderId = "simple-test-" + DateTime.UtcNow.Ticks,
                ReminderTitle = "Simple Test Reminder",
                CompletedAt = DateTime.UtcNow,
                CompletionDate = DateTime.UtcNow.Date,
                DeviceId = "debug-device"
            };

            _logger.LogInformation("Attempting simple insert for user {UserId}", userId);
            
            var success = await _mongoDbService.SyncReminderCompletionsAsync(userId, new List<CreateReminderCompletionRequest> { completion });
            
            return Ok(new { 
                success = success,
                completion = completion,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simple insert test failed");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Debug endpoint to clean corrupted records with null IDs
    /// </summary>
    [HttpDelete("debug/clean-null-ids")]
    public async Task<ActionResult> CleanNullIds()
    {
        try
        {
            var result = await _mongoDbService.CleanNullIdRecordsAsync();
            
            return Ok(new { 
                message = "Cleanup completed",
                deletedCount = result,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Null ID cleanup failed");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
} 