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
                NotificationTimes = request.NotificationTimes
            };

            await _db.CreateReminderAsync(reminder);
            return Ok(reminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reminder");
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
            reminder.NotificationTimes = request.NotificationTimes;
            
            await _db.UpdateReminderAsync(reminder);
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
            return Ok(new { message = "Action logged" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging reminder action");
            return StatusCode(500, new { message = "Server error" });
        }
    }
} 