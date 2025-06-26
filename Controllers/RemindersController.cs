using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/reminders")]
public class RemindersController : ControllerBase
{
    private readonly ILogger<RemindersController> _logger;
    private readonly MongoDbService _mongoDbService;

    public RemindersController(ILogger<RemindersController> logger, MongoDbService mongoDbService)
    {
        _logger = logger;
        _mongoDbService = mongoDbService;
    }
    
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllReminders()
    {
        try
        {
            var reminders = await _mongoDbService.GetAllRemindersAsync();
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all reminders");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetRemindersByUserId(string userId)
    {
        try
        {
            // Only allow users to access their own reminders or admins to access any reminders
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var reminders = await _mongoDbService.GetRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/active")]
    [Authorize]
    public async Task<IActionResult> GetActiveRemindersByUserId(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var reminders = await _mongoDbService.GetActiveRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/completed")]
    [Authorize]
    public async Task<IActionResult> GetCompletedRemindersByUserId(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var reminders = await _mongoDbService.GetCompletedRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completed reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/high-priority")]
    [Authorize]
    public async Task<IActionResult> GetHighPriorityRemindersByUserId(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var reminders = await _mongoDbService.GetHighPriorityRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting high priority reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/today")]
    [Authorize]
    public async Task<IActionResult> GetTodayRemindersByUserId(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var reminders = await _mongoDbService.GetTodayRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/stats")]
    [Authorize]
    public async Task<IActionResult> GetReminderStats(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var stats = await _mongoDbService.GetReminderStatsAsync(userId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder stats for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetReminderById(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            // Only allow users to access their own reminders or admins to access any reminders
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (reminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            return Ok(reminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReminder([FromBody] CreateReminderDto createReminderDto)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Get user to set username
            var user = await _mongoDbService.GetUserByIdAsync(currentUserId);
            if (user == null)
            {
                return Unauthorized(new { message = "Gebruiker niet gevonden" });
            }
            
            // Create reminder from DTO
            var reminder = new Reminder
            {
                Title = createReminderDto.Title,
                Description = createReminderDto.Description,
                Category = createReminderDto.Category ?? "General",
                Priority = createReminderDto.Priority,
                DueDate = createReminderDto.DueDate,
                ReminderType = createReminderDto.ReminderType,
                ReminderTime = createReminderDto.ReminderTime,
                ReminderStartTime = createReminderDto.ReminderStartTime,
                ReminderEndTime = createReminderDto.ReminderEndTime,
                IsRecurring = createReminderDto.IsRecurring ?? false,
                Tags = createReminderDto.Tags ?? new List<string>(),
                UserId = currentUserId,
                Username = user.Username ?? user.Email,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsCompleted = false,
                ReminderCount = 0
            };
            
            await _mongoDbService.CreateReminderAsync(reminder);
            
            return Ok(new { success = true, message = "Herinnering succesvol aangemaakt", reminder });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reminder");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateReminder(string id, [FromBody] UpdateReminderDto updateReminderDto)
    {
        try
        {
            var existingReminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (existingReminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            // Only allow users to update their own reminders or admins to update any reminders
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (existingReminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            // Update only provided fields
            if (updateReminderDto.Title != null) existingReminder.Title = updateReminderDto.Title;
            if (updateReminderDto.Description != null) existingReminder.Description = updateReminderDto.Description;
            if (updateReminderDto.IsCompleted.HasValue) existingReminder.IsCompleted = updateReminderDto.IsCompleted.Value;
            if (updateReminderDto.Category != null) existingReminder.Category = updateReminderDto.Category;
            if (updateReminderDto.Priority.HasValue) existingReminder.Priority = updateReminderDto.Priority.Value;
            if (updateReminderDto.DueDate.HasValue) existingReminder.DueDate = updateReminderDto.DueDate;
            if (updateReminderDto.ReminderType != null) existingReminder.ReminderType = updateReminderDto.ReminderType;
            if (updateReminderDto.ReminderTime != null) existingReminder.ReminderTime = updateReminderDto.ReminderTime;
            if (updateReminderDto.ReminderStartTime != null) existingReminder.ReminderStartTime = updateReminderDto.ReminderStartTime;
            if (updateReminderDto.ReminderEndTime != null) existingReminder.ReminderEndTime = updateReminderDto.ReminderEndTime;
            if (updateReminderDto.IsRecurring.HasValue) existingReminder.IsRecurring = updateReminderDto.IsRecurring.Value;
            if (updateReminderDto.IsActive.HasValue) existingReminder.IsActive = updateReminderDto.IsActive.Value;
            if (updateReminderDto.Tags != null) existingReminder.Tags = updateReminderDto.Tags;
            
            existingReminder.UpdatedAt = DateTime.UtcNow;
            
            await _mongoDbService.UpdateReminderAsync(id, existingReminder);
            
            return Ok(existingReminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/complete")]
    [Authorize]
    public async Task<IActionResult> MarkReminderAsComplete(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            // Only allow users to complete their own reminders or admins to complete any reminders
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (reminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            await _mongoDbService.UpdateReminderCompletionAsync(id, true);
            
            return Ok(new { message = "Herinnering gemarkeerd als voltooid" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking reminder {ReminderId} as complete", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/uncomplete")]
    [Authorize]
    public async Task<IActionResult> MarkReminderAsIncomplete(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (reminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            await _mongoDbService.UpdateReminderCompletionAsync(id, false);
            
            return Ok(new { message = "Herinnering gemarkeerd als niet voltooid" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking reminder {ReminderId} as incomplete", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/activate")]
    [Authorize]
    public async Task<IActionResult> ActivateReminder(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (reminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            await _mongoDbService.UpdateReminderActiveStatusAsync(id, true);
            
            return Ok(new { message = "Herinnering geactiveerd" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/deactivate")]
    [Authorize]
    public async Task<IActionResult> DeactivateReminder(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (reminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            await _mongoDbService.UpdateReminderActiveStatusAsync(id, false);
            
            return Ok(new { message = "Herinnering gedeactiveerd" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }

    [HttpPatch("{id}/snooze")]
    [Authorize]
    public async Task<IActionResult> SnoozeReminder(string id, [FromBody] SnoozeReminderDto snoozeDto)
    {
        try
        {
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (reminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            // Calculate snooze time (default 30 minutes if not specified)
            var snoozeMinutes = snoozeDto?.SnoozeMinutes ?? 30;
            var snoozeUntil = DateTime.UtcNow.AddMinutes(snoozeMinutes);
            
            // Snooze the reminder
            await _mongoDbService.SnoozeReminderAsync(id, snoozeUntil);
            
            // Increment reminder count (this represents a snooze action, not a sent reminder)
            await _mongoDbService.IncrementReminderCountAsync(id);
            
            return Ok(new { 
                message = $"Herinnering uitgesteld voor {snoozeMinutes} minuten", 
                snoozeUntil = snoozeUntil,
                snoozeMinutes = snoozeMinutes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error snoozing reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteReminder(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetReminderByIdAsync(id);
            
            if (reminder == null)
            {
                return NotFound(new { message = "Herinnering niet gevonden" });
            }
            
            // Only allow users to delete their own reminders or admins to delete any reminders
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (reminder.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            await _mongoDbService.DeleteReminderAsync(id);
            
            return Ok(new { message = "Herinnering verwijderd" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
} 