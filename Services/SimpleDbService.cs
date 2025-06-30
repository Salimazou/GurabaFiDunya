using MongoDB.Driver;
using server.Models;
using BCrypt.Net;

namespace server.Services;

public class SimpleDbService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Reminder> _reminders;
    private readonly IMongoCollection<ReminderLog> _reminderLogs;
    private readonly IMongoCollection<UserStreak> _userStreaks;
    private readonly IMongoCollection<FavoriteReciter> _favoriteReciters;
    
    public SimpleDbService(IConfiguration config)
    {
        var client = new MongoClient(config["MongoDB:ConnectionString"]);
        _database = client.GetDatabase(config["MongoDB:DatabaseName"]);
        
        _users = _database.GetCollection<User>("users");
        _reminders = _database.GetCollection<Reminder>("reminders");
        _reminderLogs = _database.GetCollection<ReminderLog>("reminderLogs");
        _userStreaks = _database.GetCollection<UserStreak>("userStreaks");
        _favoriteReciters = _database.GetCollection<FavoriteReciter>("favoriteReciters");
    }
    
    // USER OPERATIONS
    public async Task<User?> GetUserByEmailAsync(string email) =>
        await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
    
    public async Task<User?> GetUserByIdAsync(string id) =>
        await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
    
    public async Task CreateUserAsync(User user)
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
        await _users.InsertOneAsync(user);
    }
    
    public bool VerifyPassword(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
    
    // REMINDER OPERATIONS
    public async Task<List<Reminder>> GetUserRemindersAsync(string userId) =>
        await _reminders.Find(r => r.UserId == userId).ToListAsync();
    
    public async Task<Reminder?> GetReminderAsync(string id) =>
        await _reminders.Find(r => r.Id == id).FirstOrDefaultAsync();
    
    public async Task CreateReminderAsync(Reminder reminder) =>
        await _reminders.InsertOneAsync(reminder);
    
    public async Task UpdateReminderAsync(Reminder reminder) =>
        await _reminders.ReplaceOneAsync(r => r.Id == reminder.Id, reminder);
    
    public async Task DeleteReminderAsync(string id) =>
        await _reminders.DeleteOneAsync(r => r.Id == id);
    
    // NEW: Bulk update reminders for daily reset functionality
    public async Task BulkUpdateRemindersAsync(List<Reminder> reminders)
    {
        if (!reminders.Any()) return;
        
        var updates = new List<WriteModel<Reminder>>();
        foreach (var reminder in reminders)
        {
            var filter = Builders<Reminder>.Filter.Eq(r => r.Id, reminder.Id);
            var update = new ReplaceOneModel<Reminder>(filter, reminder);
            updates.Add(update);
        }
        
        if (updates.Any())
        {
            await _reminders.BulkWriteAsync(updates);
        }
    }
    
    // REMINDER LOG OPERATIONS (For Admin)
    public async Task LogReminderActionAsync(string userId, string reminderId, string reminderTitle, string action, string? deviceId = null)
    {
        var log = new ReminderLog
        {
            UserId = userId,
            ReminderId = reminderId,
            ReminderTitle = reminderTitle,
            Action = action,
            DeviceId = deviceId
        };
        await _reminderLogs.InsertOneAsync(log);
        
        // Update streak if action is "done" or "completed"
        if (action == "done" || action == "completed")
        {
            await UpdateUserStreakAsync(userId);
        }
    }
    
    public async Task<List<ReminderLog>> GetReminderLogsAsync(string? userId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var filter = Builders<ReminderLog>.Filter.Empty;
        
        if (!string.IsNullOrEmpty(userId))
            filter &= Builders<ReminderLog>.Filter.Eq(r => r.UserId, userId);
            
        if (startDate.HasValue)
            filter &= Builders<ReminderLog>.Filter.Gte(r => r.Timestamp, startDate.Value);
            
        if (endDate.HasValue)
            filter &= Builders<ReminderLog>.Filter.Lte(r => r.Timestamp, endDate.Value);
        
        return await _reminderLogs.Find(filter).SortByDescending(r => r.Timestamp).ToListAsync();
    }
    
    // NEW: Get reminder logs for specific user (used in controller)
    public async Task<List<ReminderLog>> GetUserReminderLogsAsync(string userId) =>
        await GetReminderLogsAsync(userId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
    
    // STREAK OPERATIONS - Made public for controller access
    public async Task UpdateUserStreakAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var streak = await _userStreaks.Find(s => s.UserId == userId).FirstOrDefaultAsync();
        
        if (streak == null)
        {
            streak = new UserStreak { UserId = userId };
            await _userStreaks.InsertOneAsync(streak);
        }
        
        // Check if already completed today
        if (streak.LastCompletionDate?.Date == today)
            return;
        
        streak.TotalCompletions++;
        
        // Check streak logic
        if (streak.LastCompletionDate?.Date == today.AddDays(-1))
        {
            // Consecutive day - extend streak
            streak.CurrentStreak++;
        }
        else if (streak.LastCompletionDate?.Date < today.AddDays(-1))
        {
            // Gap in streak - reset to 1
            streak.CurrentStreak = 1;
        }
        
        // Update longest streak
        if (streak.CurrentStreak > streak.LongestStreak)
            streak.LongestStreak = streak.CurrentStreak;
        
        streak.LastCompletionDate = today;
        
        await _userStreaks.ReplaceOneAsync(s => s.Id == streak.Id, streak);
    }
    
    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int limit = 10)
    {
        var streaks = await _userStreaks.Find(_ => true)
            .SortByDescending(s => s.CurrentStreak)
            .ThenByDescending(s => s.TotalCompletions)
            .Limit(limit)
            .ToListAsync();
        
        var leaderboard = new List<LeaderboardEntry>();
        int rank = 1;
        
        foreach (var streak in streaks)
        {
            var user = await GetUserByIdAsync(streak.UserId);
            if (user != null)
            {
                leaderboard.Add(new LeaderboardEntry
                {
                    UserId = streak.UserId,
                    Username = user.Username,
                    CurrentStreak = streak.CurrentStreak,
                    TotalCompletions = streak.TotalCompletions,
                    Rank = rank++
                });
            }
        }
        
        return leaderboard;
    }
    
    public async Task<UserStreak?> GetUserStreakAsync(string userId) =>
        await _userStreaks.Find(s => s.UserId == userId).FirstOrDefaultAsync();
    
    // FAVORITE RECITERS (Keep existing logic - it's fine)
    public async Task<List<FavoriteReciter>> GetUserFavoriteRecitersAsync(string userId) =>
        await _favoriteReciters.Find(f => f.UserId == userId).SortBy(f => f.Order).ToListAsync();
    
    public async Task AddFavoriteReciterAsync(string userId, string reciterId)
    {
        var existing = await _favoriteReciters.Find(f => f.UserId == userId && f.ReciterId == reciterId).FirstOrDefaultAsync();
        if (existing != null) return;
        
        var maxOrder = await _favoriteReciters.Find(f => f.UserId == userId)
            .SortByDescending(f => f.Order)
            .Project(f => f.Order)
            .FirstOrDefaultAsync();
        
        var favorite = new FavoriteReciter
        {
            UserId = userId,
            ReciterId = reciterId,
            Order = maxOrder + 1
        };
        
        await _favoriteReciters.InsertOneAsync(favorite);
    }
    
    public async Task RemoveFavoriteReciterAsync(string userId, string reciterId) =>
        await _favoriteReciters.DeleteOneAsync(f => f.UserId == userId && f.ReciterId == reciterId);
} 