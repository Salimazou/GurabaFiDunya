using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RemindersController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<RemindersController> _logger;

    public RemindersController(MongoDbService mongoDbService, ILogger<RemindersController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    private string GetUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID claim not found in token");
        }
        return userId;
    }

    // GET /api/reminders
    [HttpGet]
    public async Task<ActionResult<List<Reminder>>> GetReminders(
        [FromQuery] string? status = null, 
        [FromQuery] bool today = false)
    {
        try
        {
            var userId = GetUserId();
            
            List<Reminder> reminders;
            
            if (status == "active")
            {
                reminders = await _mongoDbService.GetActiveRemindersByUserIdAsync(userId);
            }
            else
            {
                reminders = await _mongoDbService.GetRemindersByUserIdAsync(userId);
            }
            
            // Filter for today if requested
            if (today)
            {
                var now = DateTime.UtcNow;
                var currentTime = now.TimeOfDay;
                
                reminders = reminders.Where(r => 
                    r.StartTime <= currentTime && 
                    r.EndTime >= currentTime).ToList();
            }
            
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminders for user");
            return StatusCode(500, "Internal server error");
        }
    }

    // GET /api/reminders/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Reminder>> GetReminder(string id)
    {
        try
        {
            var userId = GetUserId();
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound("Reminder not found");
            }
            
            if (reminder.UserId != userId)
            {
                return Forbid("Access denied");
            }
            
            return Ok(reminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder {ReminderId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // POST /api/reminders
    [HttpPost]
    public async Task<ActionResult<Reminder>> CreateReminder([FromBody] CreateReminderRequest request)
    {
        try
        {
            var userId = GetUserId();
            
            var reminder = new Reminder
            {
                UserId = userId,
                Title = request.Title,
                Description = request.Description,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Frequency = request.Frequency,
                Type = request.Type,
                MaxRemindersPerDay = request.MaxRemindersPerDay
            };
            
            await _mongoDbService.CreateReminderAsync(reminder);
            
            _logger.LogInformation("Created reminder {ReminderId} for user {UserId}", reminder.Id, userId);
            
            return CreatedAtAction(nameof(GetReminder), new { id = reminder.Id }, reminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reminder");
            return StatusCode(500, "Internal server error");
        }
    }

    // PATCH /api/reminders/{id}/interact
    [HttpPatch("{id}/interact")]
    public async Task<IActionResult> InteractWithReminder(string id, [FromBody] ReminderInteractionRequest request)
    {
        try
        {
            var userId = GetUserId();
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound("Reminder not found");
            }
            
            if (reminder.UserId != userId)
            {
                return Forbid("Access denied");
            }
            
            // Update reminder based on action
            await _mongoDbService.UpdateReminderInteractionAsync(id, request.Action, request.Notes);
            
            // Log the interaction
            var interaction = new ReminderInteraction
            {
                ReminderId = id,
                UserId = userId,
                Action = request.Action,
                Notes = request.Notes
            };
            
            await _mongoDbService.CreateReminderInteractionAsync(interaction);
            
            _logger.LogInformation("User {UserId} performed action {Action} on reminder {ReminderId}", 
                userId, request.Action, id);
            
            return Ok(new { message = "Interaction recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording reminder interaction");
            return StatusCode(500, "Internal server error");
        }
    }

    // PUT /api/reminders/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReminder(string id, [FromBody] UpdateReminderRequest request)
    {
        try
        {
            var userId = GetUserId();
            var existingReminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (existingReminder == null)
            {
                return NotFound("Reminder not found");
            }
            
            if (existingReminder.UserId != userId)
            {
                return Forbid("Access denied");
            }
            
            // Update properties
            if (!string.IsNullOrEmpty(request.Title))
                existingReminder.Title = request.Title;
            if (!string.IsNullOrEmpty(request.Description))
                existingReminder.Description = request.Description;
            if (request.StartTime.HasValue)
                existingReminder.StartTime = request.StartTime.Value;
            if (request.EndTime.HasValue)
                existingReminder.EndTime = request.EndTime.Value;
            if (request.Frequency.HasValue)
                existingReminder.Frequency = request.Frequency.Value;
            if (request.Type.HasValue)
                existingReminder.Type = request.Type.Value;
            if (request.IsActive.HasValue)
                existingReminder.IsActive = request.IsActive.Value;
            if (request.MaxRemindersPerDay.HasValue)
                existingReminder.MaxRemindersPerDay = request.MaxRemindersPerDay.Value;
            
            await _mongoDbService.UpdateReminderAsync(id, existingReminder);
            
            _logger.LogInformation("Updated reminder {ReminderId} for user {UserId}", id, userId);
            
            return Ok(existingReminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reminder {ReminderId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // DELETE /api/reminders/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReminder(string id)
    {
        try
        {
            var userId = GetUserId();
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound("Reminder not found");
            }
            
            if (reminder.UserId != userId)
            {
                return Forbid("Access denied");
            }
            
            await _mongoDbService.DeleteReminderAsync(id);
            
            _logger.LogInformation("Deleted reminder {ReminderId} for user {UserId}", id, userId);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reminder {ReminderId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // GET /api/reminders/stats
    [HttpGet("stats")]
    public async Task<ActionResult<ReminderStats>> GetStats()
    {
        try
        {
            var userId = GetUserId();
            var stats = await _mongoDbService.GetReminderStatsAsync(userId);
            
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder stats for user");
            return StatusCode(500, "Internal server error");
        }
    }

    // GET /api/reminders/interactions
    [HttpGet("interactions")]
    public async Task<ActionResult<List<ReminderInteraction>>> GetInteractions(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var userId = GetUserId();
            var interactions = await _mongoDbService.GetReminderInteractionsByUserIdAsync(userId, startDate, endDate);
            
            return Ok(interactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder interactions for user");
            return StatusCode(500, "Internal server error");
        }
    }

    // POST /api/reminders/sync-offline
    [HttpPost("sync-offline")]
    public async Task<IActionResult> SyncOfflineInteractions([FromBody] List<OfflineInteraction> interactions)
    {
        try
        {
            var userId = GetUserId();
            await _mongoDbService.SyncOfflineInteractionsAsync(userId, interactions);
            
            _logger.LogInformation("Synced {Count} offline interactions for user {UserId}", 
                interactions.Count, userId);
            
            return Ok(new { message = $"Synced {interactions.Count} interactions successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing offline interactions");
            return StatusCode(500, "Internal server error");
        }
    }

    // POST /api/reminders/reset-daily
    [HttpPost("reset-daily")]
    public async Task<IActionResult> ResetDailyCompletions()
    {
        try
        {
            await _mongoDbService.ResetDailyReminderCompletionsAsync();
            
            _logger.LogInformation("Reset daily reminder completions");
            
            return Ok(new { message = "Daily completions reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting daily completions");
            return StatusCode(500, "Internal server error");
        }
    }
}

// Request DTOs
public class CreateReminderRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public ReminderFrequency Frequency { get; set; }
    public ReminderType Type { get; set; }
    public int MaxRemindersPerDay { get; set; } = 3;
}

public class UpdateReminderRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public ReminderFrequency? Frequency { get; set; }
    public ReminderType? Type { get; set; }
    public bool? IsActive { get; set; }
    public int? MaxRemindersPerDay { get; set; }
}

public class ReminderInteractionRequest
{
    public ReminderAction Action { get; set; }
    public string? Notes { get; set; }
} 