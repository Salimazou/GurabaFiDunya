using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Bson;
using GurabaFiDunya.Models;
using GurabaFiDunya.Services;
using GurabaFiDunya.DTOs;

namespace GurabaFiDunya.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaderboardController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    
    public LeaderboardController(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }
    
    private string GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            // Get top users by streak count
            var totalUsers = await _databaseService.Users.CountDocumentsAsync(FilterDefinition<User>.Empty);
            
            var topUsers = await _databaseService.Users
                .Find(FilterDefinition<User>.Empty)
                .Sort(Builders<User>.Sort.Descending(u => u.StreakCount).Descending(u => u.LastActiveDate))
                .Skip((page - 1) * limit)
                .Limit(limit)
                .ToListAsync();
            
            // Calculate ranks
            var entries = topUsers.Select((user, index) => new LeaderboardEntry
            {
                UserId = user.Id,
                Name = user.Name,
                StreakCount = user.StreakCount,
                LastActiveDate = user.LastActiveDate,
                Rank = (page - 1) * limit + index + 1
            }).ToList();
            
            // Get current user's rank
            var currentUser = await _databaseService.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();
                
            var userRank = 1;
            if (currentUser != null)
            {
                var usersWithHigherStreaks = await _databaseService.Users
                    .CountDocumentsAsync(u => u.StreakCount > currentUser.StreakCount ||
                                            (u.StreakCount == currentUser.StreakCount && u.LastActiveDate > currentUser.LastActiveDate));
                userRank = (int)usersWithHigherStreaks + 1;
            }
            
            var response = new LeaderboardResponse
            {
                Entries = entries,
                TotalUsers = (int)totalUsers,
                UserRank = userRank,
                LastUpdated = DateTime.UtcNow
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserRank(string userId)
    {
        try
        {
            var currentUserId = GetUserId();
            if (string.IsNullOrEmpty(currentUserId))
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
            
            var usersWithHigherStreaks = await _databaseService.Users
                .CountDocumentsAsync(u => u.StreakCount > user.StreakCount ||
                                        (u.StreakCount == user.StreakCount && u.LastActiveDate > user.LastActiveDate));
            
            var userRank = (int)usersWithHigherStreaks + 1;
            
            var entry = new LeaderboardEntry
            {
                UserId = user.Id,
                Name = user.Name,
                StreakCount = user.StreakCount,
                LastActiveDate = user.LastActiveDate,
                Rank = userRank
            };
            
            return Ok(entry);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var totalUsers = await _databaseService.Users.CountDocumentsAsync(FilterDefinition<User>.Empty);
            
            var topStreak = await _databaseService.Users
                .Find(FilterDefinition<User>.Empty)
                .Sort(Builders<User>.Sort.Descending(u => u.StreakCount))
                .Limit(1)
                .FirstOrDefaultAsync();
                
            var currentUser = await _databaseService.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();
            
            var averageStreak = await _databaseService.Users
                .Aggregate()
                .Group(new BsonDocument { { "_id", BsonNull.Value }, { "avgStreak", new BsonDocument("$avg", "$streakCount") } })
                .FirstOrDefaultAsync();
            
            var stats = new
            {
                TotalUsers = totalUsers,
                TopStreak = topStreak?.StreakCount ?? 0,
                AverageStreak = averageStreak?["avgStreak"]?.AsDouble ?? 0,
                UserStreak = currentUser?.StreakCount ?? 0,
                LastUpdated = DateTime.UtcNow
            };
            
            return Ok(stats);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
} 