using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/simple-reminders")]
public class SimpleRemindersController : ControllerBase
{
    private readonly ILogger<SimpleRemindersController> _logger;
    private readonly MongoDbService _mongoDbService;

    public SimpleRemindersController(ILogger<SimpleRemindersController> logger, MongoDbService mongoDbService)
    {
        _logger = logger;
        _mongoDbService = mongoDbService;
    }
    
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllSimpleReminders()
    {
        try
        {
            var reminders = await _mongoDbService.GetAllSimpleRemindersAsync();
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all simple reminders");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetSimpleRemindersByUserId(string userId)
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
            
            var reminders = await _mongoDbService.GetSimpleRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting simple reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/active")]
    [Authorize]
    public async Task<IActionResult> GetActiveSimpleRemindersByUserId(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var reminders = await _mongoDbService.GetActiveSimpleRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active simple reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/completed")]
    [Authorize]
    public async Task<IActionResult> GetCompletedSimpleRemindersByUserId(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var reminders = await _mongoDbService.GetCompletedSimpleRemindersByUserIdAsync(userId);
            return Ok(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completed simple reminders for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}/stats")]
    [Authorize]
    public async Task<IActionResult> GetSimpleReminderStats(string userId)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var stats = await _mongoDbService.GetSimpleReminderStatsAsync(userId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting simple reminder stats for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetSimpleReminderById(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetSimpleReminderByIdAsync(id);
            
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
            _logger.LogError(ex, "Error getting simple reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateSimpleReminder([FromBody] CreateSimpleReminderDto createReminderDto)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Validate reminder type
            if (!IsValidReminderType(createReminderDto.Type))
            {
                return BadRequest(new { message = "Ongeldig reminder type. Geldige types: daily, custom_time, specific_time" });
            }
            
            // Validate time settings based on type
            if (!ValidateTimeSettings(createReminderDto))
            {
                return BadRequest(new { message = "Ongeldige tijdinstellingen voor het gekozen type" });
            }
            
            // Create reminder
            var reminder = new SimpleReminder
            {
                UserId = currentUserId,
                Title = createReminderDto.Title,
                Description = createReminderDto.Description,
                Type = createReminderDto.Type,
                SpecificTime = createReminderDto.SpecificTime,
                CustomTimeRanges = createReminderDto.CustomTimeRanges
            };
            
            await _mongoDbService.CreateSimpleReminderAsync(reminder);
            
            return Ok(new { success = true, message = "Herinnering succesvol aangemaakt", reminder });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating simple reminder");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateSimpleReminder(string id, [FromBody] UpdateSimpleReminderDto updateReminderDto)
    {
        try
        {
            var existingReminder = await _mongoDbService.GetSimpleReminderByIdAsync(id);
            
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
            if (updateReminderDto.IsActive.HasValue) existingReminder.IsActive = updateReminderDto.IsActive.Value;
            if (updateReminderDto.SpecificTime != null) existingReminder.SpecificTime = updateReminderDto.SpecificTime;
            if (updateReminderDto.CustomTimeRanges != null) existingReminder.CustomTimeRanges = updateReminderDto.CustomTimeRanges;
            
            existingReminder.UpdatedAt = DateTime.UtcNow;
            
            await _mongoDbService.UpdateSimpleReminderAsync(id, existingReminder);
            
            return Ok(existingReminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating simple reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/complete")]
    [Authorize]
    public async Task<IActionResult> MarkSimpleReminderAsComplete(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetSimpleReminderByIdAsync(id);
            
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
            
            await _mongoDbService.UpdateSimpleReminderCompletionAsync(id, true);
            
            return Ok(new { message = "Herinnering gemarkeerd als voltooid" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking simple reminder {ReminderId} as complete", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/uncomplete")]
    [Authorize]
    public async Task<IActionResult> MarkSimpleReminderAsIncomplete(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetSimpleReminderByIdAsync(id);
            
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
            
            await _mongoDbService.UpdateSimpleReminderCompletionAsync(id, false);
            
            return Ok(new { message = "Herinnering gemarkeerd als niet voltooid" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking simple reminder {ReminderId} as incomplete", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/activate")]
    [Authorize]
    public async Task<IActionResult> ActivateSimpleReminder(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetSimpleReminderByIdAsync(id);
            
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
            
            await _mongoDbService.UpdateSimpleReminderActiveStatusAsync(id, true);
            
            return Ok(new { message = "Herinnering geactiveerd" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating simple reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/deactivate")]
    [Authorize]
    public async Task<IActionResult> DeactivateSimpleReminder(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetSimpleReminderByIdAsync(id);
            
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
            
            await _mongoDbService.UpdateSimpleReminderActiveStatusAsync(id, false);
            
            return Ok(new { message = "Herinnering gedeactiveerd" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating simple reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteSimpleReminder(string id)
    {
        try
        {
            var reminder = await _mongoDbService.GetSimpleReminderByIdAsync(id);
            
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
            
            await _mongoDbService.DeleteSimpleReminderAsync(id);
            
            return Ok(new { message = "Herinnering verwijderd" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting simple reminder {ReminderId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    // Helper methods
    private bool IsValidReminderType(string type)
    {
        return type == "daily" || type == "custom_time" || type == "specific_time";
    }
    
    private bool ValidateTimeSettings(CreateSimpleReminderDto dto)
    {
        switch (dto.Type)
        {
            case "daily":
                // Daily reminders don't need specific time settings
                return true;
                
            case "custom_time":
                // Custom time reminders need at least one time range
                return dto.CustomTimeRanges != null && dto.CustomTimeRanges.Count > 0 &&
                       dto.CustomTimeRanges.All(r => r.StartTime < r.EndTime);
                
            case "specific_time":
                // Specific time reminders need a specific time
                return dto.SpecificTime.HasValue && dto.SpecificTime.Value > DateTime.UtcNow;
                
            default:
                return false;
        }
    }
} 