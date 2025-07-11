using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using GurabaFiDunya.Models;
using GurabaFiDunya.Services;
using GurabaFiDunya.DTOs;

namespace GurabaFiDunya.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RemindersController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    
    public RemindersController(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }
    
    private string GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetReminders()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var reminders = await _databaseService.Reminders
                .Find(r => r.UserId == userId)
                .ToListAsync();
                
            var response = reminders.Select(r => new ReminderResponse
            {
                Id = r.Id,
                UserId = r.UserId,
                Title = r.Title,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Frequency = r.Frequency,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateReminder([FromBody] CreateReminderRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            if (string.IsNullOrEmpty(request.Title))
            {
                return BadRequest("Title is required");
            }
            
            var reminder = new Reminder
            {
                UserId = userId,
                Title = request.Title,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Frequency = request.Frequency,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await _databaseService.Reminders.InsertOneAsync(reminder);
            
            var response = new ReminderResponse
            {
                Id = reminder.Id,
                UserId = reminder.UserId,
                Title = reminder.Title,
                StartTime = reminder.StartTime,
                EndTime = reminder.EndTime,
                Frequency = reminder.Frequency,
                IsActive = reminder.IsActive,
                CreatedAt = reminder.CreatedAt,
                UpdatedAt = reminder.UpdatedAt
            };
            
            return CreatedAtAction(nameof(GetReminder), new { id = reminder.Id }, response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetReminder(string id)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var reminder = await _databaseService.Reminders
                .Find(r => r.Id == id && r.UserId == userId)
                .FirstOrDefaultAsync();
                
            if (reminder == null)
            {
                return NotFound("Reminder not found");
            }
            
            var response = new ReminderResponse
            {
                Id = reminder.Id,
                UserId = reminder.UserId,
                Title = reminder.Title,
                StartTime = reminder.StartTime,
                EndTime = reminder.EndTime,
                Frequency = reminder.Frequency,
                IsActive = reminder.IsActive,
                CreatedAt = reminder.CreatedAt,
                UpdatedAt = reminder.UpdatedAt
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReminder(string id, [FromBody] UpdateReminderRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var reminder = await _databaseService.Reminders
                .Find(r => r.Id == id && r.UserId == userId)
                .FirstOrDefaultAsync();
                
            if (reminder == null)
            {
                return NotFound("Reminder not found");
            }
            
            if (!string.IsNullOrEmpty(request.Title))
            {
                reminder.Title = request.Title;
            }
            
            reminder.StartTime = request.StartTime;
            reminder.EndTime = request.EndTime;
            reminder.Frequency = request.Frequency;
            reminder.IsActive = request.IsActive;
            reminder.UpdatedAt = DateTime.UtcNow;
            
            await _databaseService.Reminders.ReplaceOneAsync(r => r.Id == id, reminder);
            
            var response = new ReminderResponse
            {
                Id = reminder.Id,
                UserId = reminder.UserId,
                Title = reminder.Title,
                StartTime = reminder.StartTime,
                EndTime = reminder.EndTime,
                Frequency = reminder.Frequency,
                IsActive = reminder.IsActive,
                CreatedAt = reminder.CreatedAt,
                UpdatedAt = reminder.UpdatedAt
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReminder(string id)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var result = await _databaseService.Reminders
                .DeleteOneAsync(r => r.Id == id && r.UserId == userId);
                
            if (result.DeletedCount == 0)
            {
                return NotFound("Reminder not found");
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpPost("{id}/mark")]
    public async Task<IActionResult> MarkReminder(string id, [FromBody] MarkReminderRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var reminder = await _databaseService.Reminders
                .Find(r => r.Id == id && r.UserId == userId)
                .FirstOrDefaultAsync();
                
            if (reminder == null)
            {
                return NotFound("Reminder not found");
            }
            
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            // Check if log already exists for today
            var existingLog = await _databaseService.ReminderLogs
                .Find(rl => rl.ReminderId == id && rl.Date == today)
                .FirstOrDefaultAsync();
                
            if (existingLog != null)
            {
                // Update existing log
                existingLog.Status = request.Status;
                existingLog.TimeMarked = DateTime.UtcNow;
                existingLog.UpdatedAt = DateTime.UtcNow;
                await _databaseService.ReminderLogs.ReplaceOneAsync(rl => rl.Id == existingLog.Id, existingLog);
            }
            else
            {
                // Create new log
                var log = new ReminderLog
                {
                    ReminderId = id,
                    UserId = userId,
                    Date = today,
                    Status = request.Status,
                    TimeMarked = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _databaseService.ReminderLogs.InsertOneAsync(log);
            }
            
            // Update user streak if reminder is completed
            if (request.Status == "completed")
            {
                await UpdateUserStreak(userId, today);
            }
            
            return Ok(new { message = "Reminder marked successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("logs")]
    public async Task<IActionResult> GetReminderLogs([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var from = fromDate?.Date ?? DateTime.UtcNow.AddDays(-30);
            var to = toDate?.Date ?? DateTime.UtcNow;
            
            var logs = await _databaseService.ReminderLogs
                .Find(rl => rl.UserId == userId && 
                           rl.Date >= DateOnly.FromDateTime(from) && 
                           rl.Date <= DateOnly.FromDateTime(to))
                .ToListAsync();
                
            var response = logs.Select(l => new ReminderLogResponse
            {
                Id = l.Id,
                ReminderId = l.ReminderId,
                UserId = l.UserId,
                Date = l.Date,
                Status = l.Status,
                TimeMarked = l.TimeMarked,
                CreatedAt = l.CreatedAt
            }).ToList();
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    private async Task UpdateUserStreak(string userId, DateOnly today)
    {
        var user = await _databaseService.Users
            .Find(u => u.Id == userId)
            .FirstOrDefaultAsync();
            
        if (user == null) return;
        
        var yesterday = today.AddDays(-1);
        var lastActiveDate = DateOnly.FromDateTime(user.LastActiveDate);
        
        if (lastActiveDate == yesterday)
        {
            // Continue streak
            user.StreakCount++;
        }
        else if (lastActiveDate == today)
        {
            // Already marked today, don't change streak
            return;
        }
        else
        {
            // Reset streak
            user.StreakCount = 1;
        }
        
        user.LastActiveDate = today.ToDateTime(TimeOnly.MinValue);
        user.UpdatedAt = DateTime.UtcNow;
        
        await _databaseService.Users.ReplaceOneAsync(u => u.Id == userId, user);
    }
} 