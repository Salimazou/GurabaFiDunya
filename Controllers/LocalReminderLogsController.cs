using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/local-reminder-logs")]
[Authorize]
public class LocalReminderLogsController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<LocalReminderLogsController> _logger;

    public LocalReminderLogsController(
        MongoDbService mongoDbService,
        ILogger<LocalReminderLogsController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    /// Log a single reminder event (notification sent, user response, etc.)
    /// </summary>
    [HttpPost("log")]
    public async Task<IActionResult> CreateLog([FromBody] CreateLocalReminderLogRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var logId = await _mongoDbService.CreateLocalReminderLogAsync(userId, request);
            
            _logger.LogInformation("Created local reminder log {LogId} for user {UserId}", logId, userId);
            
            return Ok(new { LogId = logId, Message = "Log created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating local reminder log");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Bulk log multiple notifications when they are scheduled
    /// </summary>
    [HttpPost("bulk-notifications")]
    public async Task<IActionResult> BulkLogNotifications([FromBody] BulkNotificationLogRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _mongoDbService.BulkCreateNotificationLogsAsync(userId, request);
            
            _logger.LogInformation("Bulk logged {NotificationCount} notifications for user {UserId}", 
                request.Notifications.Count, userId);
            
            return Ok(new { 
                Message = "Notifications logged successfully", 
                Count = request.Notifications.Count 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk logging notifications");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Log when user responds to a notification
    /// </summary>
    [HttpPost("log-response")]
    public async Task<IActionResult> LogUserResponse([FromBody] LogUserResponseRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var logType = request.Response.ToLower() switch
            {
                "done" => LocalReminderLogType.UserResponseDone,
                "not_yet" => LocalReminderLogType.UserResponseNotYet,
                "tomorrow" => LocalReminderLogType.UserResponseTomorrow,
                _ => throw new ArgumentException($"Invalid response type: {request.Response}")
            };

            var logRequest = new CreateLocalReminderLogRequest
            {
                ReminderId = request.ReminderId,
                ReminderTitle = request.ReminderTitle,
                LogType = logType,
                UserResponse = request.Response,
                ResponseTime = request.ResponseTime ?? DateTime.UtcNow,
                NotificationTime = request.NotificationTime,
                DeviceId = request.DeviceId,
                AppVersion = request.AppVersion,
                Metadata = request.Metadata
            };

            var logId = await _mongoDbService.CreateLocalReminderLogAsync(userId, logRequest);
            
            _logger.LogInformation("Logged user response {Response} for reminder {ReminderId}", 
                request.Response, request.ReminderId);
            
            return Ok(new { LogId = logId, Message = "User response logged successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging user response");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get logs for the current user with optional filtering
    /// </summary>
    [HttpGet("my-logs")]
    public async Task<IActionResult> GetMyLogs(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? reminderId = null,
        [FromQuery] string? logType = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            LocalReminderLogType? logTypeEnum = null;
            if (!string.IsNullOrEmpty(logType) && Enum.TryParse<LocalReminderLogType>(logType, true, out var parsedLogType))
            {
                logTypeEnum = parsedLogType;
            }

            var logs = await _mongoDbService.GetLocalReminderLogsAsync(
                userId, startDate, endDate, reminderId, logTypeEnum);
            
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local reminder logs");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get comprehensive analytics for the current user's reminder usage
    /// </summary>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetMyAnalytics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var start = startDate ?? DateTime.UtcNow.AddDays(-30); // Default to last 30 days
            var end = endDate ?? DateTime.UtcNow;

            var analytics = await _mongoDbService.GetLocalReminderAnalyticsAsync(userId, start, end);
            
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local reminder analytics");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get quick stats for the current user (for dashboard display)
    /// </summary>
    [HttpGet("quick-stats")]
    public async Task<IActionResult> GetQuickStats([FromQuery] int days = 7)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var startDate = DateTime.UtcNow.AddDays(-days);
            var endDate = DateTime.UtcNow;

            var analytics = await _mongoDbService.GetLocalReminderAnalyticsAsync(userId, startDate, endDate);
            
            var quickStats = new
            {
                Period = $"Last {days} days",
                TotalNotifications = analytics.TotalNotificationsSent,
                CompletedTasks = analytics.TotalResponsesDone,
                CompletionRate = Math.Round(analytics.CompletionRate, 1),
                AverageResponseTime = Math.Round(analytics.AverageResponseTimeMinutes, 1),
                MostEffectiveReminder = analytics.ReminderEffectiveness
                    .OrderByDescending(x => x.CompletionRate)
                    .FirstOrDefault()?.ReminderTitle ?? "None",
                DailyAverage = analytics.DailyActivity.Any() 
                    ? Math.Round(analytics.TotalNotificationsSent / (double)analytics.DailyActivity.Count, 1)
                    : 0
            };
            
            return Ok(quickStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quick stats");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get effectiveness comparison between different reminders
    /// </summary>
    [HttpGet("effectiveness")]
    public async Task<IActionResult> GetReminderEffectiveness([FromQuery] int days = 30)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var startDate = DateTime.UtcNow.AddDays(-days);
            var endDate = DateTime.UtcNow;

            var analytics = await _mongoDbService.GetLocalReminderAnalyticsAsync(userId, startDate, endDate);
            
            return Ok(analytics.ReminderEffectiveness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder effectiveness");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get daily activity trends
    /// </summary>
    [HttpGet("daily-trends")]
    public async Task<IActionResult> GetDailyTrends([FromQuery] int days = 30)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var startDate = DateTime.UtcNow.AddDays(-days);
            var endDate = DateTime.UtcNow;

            var analytics = await _mongoDbService.GetLocalReminderAnalyticsAsync(userId, startDate, endDate);
            
            return Ok(analytics.DailyActivity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily trends");
            return StatusCode(500, new { Message = "Internal server error" });
        }
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    }
}

/// <summary>
/// DTO for logging user responses to notifications
/// </summary>
public class LogUserResponseRequest
{
    public string ReminderId { get; set; } = string.Empty;
    public string ReminderTitle { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty; // "done", "not_yet", "tomorrow"
    public DateTime? ResponseTime { get; set; }
    public DateTime? NotificationTime { get; set; }
    public string? DeviceId { get; set; }
    public string? AppVersion { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
} 