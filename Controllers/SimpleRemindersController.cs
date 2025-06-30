using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize]
public class SimpleRemindersController : ControllerBase
{
    private readonly SimpleDbService _db;
    private readonly ILogger<SimpleRemindersController> _logger;

    public SimpleRemindersController(SimpleDbService db, ILogger<SimpleRemindersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> GetMyReminders()
    {
        try
        {
            var userId = GetCurrentUserId();
            var reminders = await _db.GetUserRemindersAsync(userId);
            
            // Check for daily resets
            foreach (var reminder in reminders)
            {
                reminder.CheckDailyReset();
            }
            
            // Save any updated reminders
            await _db.BulkUpdateRemindersAsync(reminders);
            
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminders");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateReminder([FromBody] CreateReminderRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reminder = new Reminder
            {
                UserId = userId,
                Title = request.Title,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsCompletedToday = false,
                CreatedAt = DateTime.UtcNow
            };

            await _db.CreateReminderAsync(reminder);
            
            // Log creation action
            await _db.LogReminderActionAsync(userId, reminder.Id, reminder.Title, "created", null);
            
            return Ok(reminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reminder");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> MarkReminderCompleted(string id, [FromBody] MarkReminderCompleteRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reminder = await _db.GetReminderAsync(id);
            
            if (reminder == null || reminder.UserId != userId)
            {
                return NotFound();
            }

            // Mark as completed for today
            reminder.IsCompletedToday = true;
            reminder.LastCompletionDate = request.CompletedAt;
            
            await _db.UpdateReminderAsync(reminder);
            
            // Log completion action
            await _db.LogReminderActionAsync(userId, id, reminder.Title, "completed", null);
            
            // Update user streak
            await _db.UpdateUserStreakAsync(userId);
            
            return Ok(new { message = "Reminder marked as completed", reminder });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking reminder completed");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReminder(string id, [FromBody] CreateReminderRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reminder = await _db.GetReminderAsync(id);
            
            if (reminder == null || reminder.UserId != userId)
            {
                return NotFound();
            }

            reminder.Title = request.Title;
            reminder.StartTime = request.StartTime;
            reminder.EndTime = request.EndTime;
            
            await _db.UpdateReminderAsync(reminder);
            
            // Log update action
            await _db.LogReminderActionAsync(userId, id, reminder.Title, "updated", null);
            
            return Ok(reminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reminder");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReminder(string id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reminder = await _db.GetReminderAsync(id);
            
            if (reminder == null || reminder.UserId != userId)
            {
                return NotFound();
            }

            await _db.DeleteReminderAsync(id);
            
            // Log deletion action
            await _db.LogReminderActionAsync(userId, id, reminder.Title, "deleted", null);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reminder");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpPost("{id}/log")]
    public async Task<IActionResult> LogReminderAction(string id, [FromBody] LogReminderActionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reminder = await _db.GetReminderAsync(id);
            
            if (reminder == null || reminder.UserId != userId)
            {
                return NotFound();
            }

            await _db.LogReminderActionAsync(userId, id, reminder.Title, request.Action, request.DeviceId);
            
            // If action is "done", also mark as completed
            if (request.Action.ToLower() == "done")
            {
                reminder.IsCompletedToday = true;
                reminder.LastCompletionDate = DateTime.UtcNow;
                await _db.UpdateReminderAsync(reminder);
                await _db.UpdateUserStreakAsync(userId);
            }
            
            return Ok(new { message = "Action logged successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging reminder action");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    // Admin endpoint to get all reminder logs
    [HttpGet("logs")]
    public async Task<IActionResult> GetReminderLogs()
    {
        try
        {
            var userId = GetCurrentUserId();
            var logs = await _db.GetUserReminderLogsAsync(userId);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder logs");
            return StatusCode(500, new { message = "Server error" });
        }
    }
} 