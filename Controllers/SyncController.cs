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
public class SyncController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    
    public SyncController(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }
    
    private string GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
    
    [HttpPost("reminders")]
    public async Task<IActionResult> SyncReminders([FromBody] SyncRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var response = new SyncResponse
            {
                ServerLogs = new List<ReminderLogResponse>(),
                UpdatedReminders = new List<ReminderResponse>(),
                ServerTime = DateTime.UtcNow,
                Success = true
            };
            
            // Process offline logs from the client
            foreach (var offlineLog in request.OfflineLogs)
            {
                try
                {
                    // Check if the reminder exists and belongs to the user
                    var reminder = await _databaseService.Reminders
                        .Find(r => r.Id == offlineLog.ReminderId && r.UserId == userId)
                        .FirstOrDefaultAsync();
                    
                    if (reminder == null)
                    {
                        continue; // Skip if reminder doesn't exist or doesn't belong to user
                    }
                    
                    // Check if a log already exists for this date
                    var existingLog = await _databaseService.ReminderLogs
                        .Find(rl => rl.ReminderId == offlineLog.ReminderId && 
                                   rl.UserId == userId && 
                                   rl.Date == offlineLog.Date)
                        .FirstOrDefaultAsync();
                    
                    if (existingLog != null)
                    {
                        // Update existing log if the offline version is newer
                        if (offlineLog.TimeMarked > existingLog.TimeMarked)
                        {
                            existingLog.Status = offlineLog.Status;
                            existingLog.TimeMarked = offlineLog.TimeMarked;
                            existingLog.UpdatedAt = DateTime.UtcNow;
                            await _databaseService.ReminderLogs.ReplaceOneAsync(
                                rl => rl.Id == existingLog.Id, existingLog);
                        }
                    }
                    else
                    {
                        // Create new log
                        var newLog = new ReminderLog
                        {
                            ReminderId = offlineLog.ReminderId,
                            UserId = userId,
                            Date = offlineLog.Date,
                            Status = offlineLog.Status,
                            TimeMarked = offlineLog.TimeMarked,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        
                        await _databaseService.ReminderLogs.InsertOneAsync(newLog);
                    }
                    
                    // Update user streak if the log is a completion
                    if (offlineLog.Status == "completed")
                    {
                        await UpdateUserStreakForSync(userId, offlineLog.Date);
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but continue processing other logs
                    Console.WriteLine($"Error processing offline log: {ex.Message}");
                }
            }
            
            // Get server logs that are newer than the client's last sync time
            var serverLogs = await _databaseService.ReminderLogs
                .Find(rl => rl.UserId == userId && rl.UpdatedAt > request.LastSyncTime)
                .ToListAsync();
            
            response.ServerLogs = serverLogs.Select(l => new ReminderLogResponse
            {
                Id = l.Id,
                ReminderId = l.ReminderId,
                UserId = l.UserId,
                Date = l.Date,
                Status = l.Status,
                TimeMarked = l.TimeMarked,
                CreatedAt = l.CreatedAt
            }).ToList();
            
            // Get updated reminders
            var updatedReminders = await _databaseService.Reminders
                .Find(r => r.UserId == userId && r.UpdatedAt > request.LastSyncTime)
                .ToListAsync();
            
            response.UpdatedReminders = updatedReminders.Select(r => new ReminderResponse
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
    
    [HttpGet("status")]
    public async Task<IActionResult> GetSyncStatus()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var user = await _databaseService.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();
            
            if (user == null)
            {
                return NotFound("User not found");
            }
            
            var remindersCount = await _databaseService.Reminders
                .CountDocumentsAsync(r => r.UserId == userId);
            
            var logsCount = await _databaseService.ReminderLogs
                .CountDocumentsAsync(rl => rl.UserId == userId);
            
            var lastLogDate = await _databaseService.ReminderLogs
                .Find(rl => rl.UserId == userId)
                .Sort(Builders<ReminderLog>.Sort.Descending(rl => rl.CreatedAt))
                .Limit(1)
                .FirstOrDefaultAsync();
            
            var status = new
            {
                UserId = userId,
                UserName = user.Name,
                StreakCount = user.StreakCount,
                LastActiveDate = user.LastActiveDate,
                RemindersCount = remindersCount,
                LogsCount = logsCount,
                LastLogDate = lastLogDate?.CreatedAt,
                ServerTime = DateTime.UtcNow
            };
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpPost("user-data")]
    public async Task<IActionResult> BackupUserData()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var user = await _databaseService.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();
            
            if (user == null)
            {
                return NotFound("User not found");
            }
            
            var reminders = await _databaseService.Reminders
                .Find(r => r.UserId == userId)
                .ToListAsync();
            
            var logs = await _databaseService.ReminderLogs
                .Find(rl => rl.UserId == userId)
                .ToListAsync();
            
            var backup = new
            {
                User = new UserProfileResponse
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    StreakCount = user.StreakCount,
                    LastActiveDate = user.LastActiveDate,
                    CreatedAt = user.CreatedAt
                },
                Reminders = reminders.Select(r => new ReminderResponse
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
                }).ToList(),
                Logs = logs.Select(l => new ReminderLogResponse
                {
                    Id = l.Id,
                    ReminderId = l.ReminderId,
                    UserId = l.UserId,
                    Date = l.Date,
                    Status = l.Status,
                    TimeMarked = l.TimeMarked,
                    CreatedAt = l.CreatedAt
                }).ToList(),
                BackupDate = DateTime.UtcNow
            };
            
            return Ok(backup);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    private async Task UpdateUserStreakForSync(string userId, DateOnly completionDate)
    {
        var user = await _databaseService.Users
            .Find(u => u.Id == userId)
            .FirstOrDefaultAsync();
        
        if (user == null) return;
        
        var lastActiveDate = DateOnly.FromDateTime(user.LastActiveDate);
        var yesterday = completionDate.AddDays(-1);
        
        // Only update if this completion date is newer than the last recorded active date
        if (completionDate > lastActiveDate)
        {
            if (lastActiveDate == yesterday)
            {
                // Continue streak
                user.StreakCount++;
            }
            else if (lastActiveDate < yesterday)
            {
                // Reset streak to 1 (there was a gap)
                user.StreakCount = 1;
            }
            // If lastActiveDate == completionDate, don't change streak
            
            user.LastActiveDate = completionDate.ToDateTime(TimeOnly.MinValue);
            user.UpdatedAt = DateTime.UtcNow;
            
            await _databaseService.Users.ReplaceOneAsync(u => u.Id == userId, user);
        }
    }
} 