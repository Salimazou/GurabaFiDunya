using MongoDB.Driver;
using MongoDB.Bson;
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

    public SimpleDbService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDB") ?? 
                              configuration["MongoDB:ConnectionString"];
        var databaseName = configuration["MongoDB:DatabaseName"];

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);

        _users = _database.GetCollection<User>("Users");
        _reminders = _database.GetCollection<Reminder>("Reminders");
        _reminderLogs = _database.GetCollection<ReminderLog>("ReminderLogs");
        _userStreaks = _database.GetCollection<UserStreak>("UserStreaks");
        _favoriteReciters = _database.GetCollection<FavoriteReciter>("FavoriteReciters");

        // Initialize database and create indexes
        InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            // Test connection and create database/collections by inserting and removing a test document
            Console.WriteLine("Initializing MongoDB database and collections...");

            // Create indexes for better performance
            await CreateIndexesAsync();

            Console.WriteLine("MongoDB database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing MongoDB: {ex.Message}");
            throw;
        }
    }

    private async Task CreateIndexesAsync()
    {
        // Create index on Users collection
        var userEmailIndex = Builders<User>.IndexKeys.Ascending(u => u.Email);
        await _users.Indexes.CreateOneAsync(new CreateIndexModel<User>(userEmailIndex, new CreateIndexOptions { Unique = true }));

        // Create indexes on Reminders collection
        var reminderUserIdIndex = Builders<Reminder>.IndexKeys.Ascending(r => r.UserId);
        await _reminders.Indexes.CreateOneAsync(new CreateIndexModel<Reminder>(reminderUserIdIndex));

        // Create indexes on FavoriteReciters collection
        var favoriteUserIdIndex = Builders<FavoriteReciter>.IndexKeys.Ascending(f => f.UserId);
        var favoriteUserReciterIndex = Builders<FavoriteReciter>.IndexKeys
            .Ascending(f => f.UserId)
            .Ascending(f => f.ReciterId);
        await _favoriteReciters.Indexes.CreateOneAsync(new CreateIndexModel<FavoriteReciter>(favoriteUserIdIndex));
        await _favoriteReciters.Indexes.CreateOneAsync(new CreateIndexModel<FavoriteReciter>(favoriteUserReciterIndex));

        // Create indexes on ReminderLogs collection
        var logUserIdIndex = Builders<ReminderLog>.IndexKeys.Ascending(l => l.UserId);
        var logTimestampIndex = Builders<ReminderLog>.IndexKeys.Descending(l => l.Timestamp);
        await _reminderLogs.Indexes.CreateOneAsync(new CreateIndexModel<ReminderLog>(logUserIdIndex));
        await _reminderLogs.Indexes.CreateOneAsync(new CreateIndexModel<ReminderLog>(logTimestampIndex));

        // Create index on UserStreaks collection
        var streakUserIdIndex = Builders<UserStreak>.IndexKeys.Ascending(s => s.UserId);
        await _userStreaks.Indexes.CreateOneAsync(new CreateIndexModel<UserStreak>(streakUserIdIndex));

        Console.WriteLine("Database indexes created successfully.");
    }

    // MARK: - User Methods
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
    }

    // FIX: Renamed parameter from 'user' to 'registerRequest' and properly handle plaintext password
    public async Task CreateUserAsync(RegisterRequest registerRequest)
    {
        var user = new User
        {
            Email = registerRequest.Email,
            Username = registerRequest.Username,
            FirstName = registerRequest.FirstName,
            LastName = registerRequest.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequest.Password), // FIX: Hash the plaintext password
            CreatedAt = DateTime.UtcNow
        };

        await _users.InsertOneAsync(user);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }

    // MARK: - Reminder Methods
    public async Task<List<Reminder>> GetUserRemindersAsync(string userId)
    {
        return await _reminders.Find(r => r.UserId == userId).ToListAsync();
    }

    public async Task<Reminder?> GetReminderAsync(string id)
    {
        return await _reminders.Find(r => r.Id == id).FirstOrDefaultAsync();
    }

    public async Task CreateReminderAsync(Reminder reminder)
    {
        await _reminders.InsertOneAsync(reminder);
    }

    public async Task UpdateReminderAsync(Reminder reminder)
    {
        await _reminders.ReplaceOneAsync(r => r.Id == reminder.Id, reminder);
    }

    public async Task DeleteReminderAsync(string id)
    {
        await _reminders.DeleteOneAsync(r => r.Id == id);
    }

    public async Task BulkUpdateRemindersAsync(List<Reminder> reminders)
    {
        var bulkOps = new List<WriteModel<Reminder>>();
        foreach (var reminder in reminders)
        {
            var filter = Builders<Reminder>.Filter.Eq(r => r.Id, reminder.Id);
            var replaceModel = new ReplaceOneModel<Reminder>(filter, reminder);
            bulkOps.Add(replaceModel);
        }

        if (bulkOps.Any())
        {
            await _reminders.BulkWriteAsync(bulkOps);
        }
    }

    // MARK: - Favorite Reciters Methods
    public async Task<List<FavoriteReciter>> GetUserFavoriteRecitersAsync(string userId)
    {
        return await _favoriteReciters
            .Find(f => f.UserId == userId)
            .SortBy(f => f.Order)
            .ToListAsync();
    }

    // FIX: Changed return type from void to bool to indicate success/failure
    public async Task<bool> AddFavoriteReciterAsync(string userId, string reciterId)
    {
        // Check if already exists
        var existing = await _favoriteReciters
            .Find(f => f.UserId == userId && f.ReciterId == reciterId)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return false; // FIX: Return false to indicate already exists
        }

        // Get current max order for user
        var maxOrder = await _favoriteReciters
            .Find(f => f.UserId == userId)
            .SortByDescending(f => f.Order)
            .Project(f => f.Order)
            .FirstOrDefaultAsync();

        var favorite = new FavoriteReciter
        {
            UserId = userId,
            ReciterId = reciterId,
            AddedAt = DateTime.UtcNow,
            Order = maxOrder + 1
        };

        await _favoriteReciters.InsertOneAsync(favorite);
        return true; // FIX: Return true to indicate success
    }

    public async Task RemoveFavoriteReciterAsync(string userId, string reciterId)
    {
        await _favoriteReciters.DeleteOneAsync(f => f.UserId == userId && f.ReciterId == reciterId);
    }

    // MARK: - Reminder Logging
    public async Task LogReminderActionAsync(string userId, string reminderId, string reminderTitle, string action, string? deviceId)
    {
        var log = new ReminderLog
        {
            UserId = userId,
            ReminderId = reminderId,
            ReminderTitle = reminderTitle,
            Action = action,
            Timestamp = DateTime.UtcNow,
            DeviceId = deviceId
        };

        await _reminderLogs.InsertOneAsync(log);
    }

    public async Task<List<ReminderLog>> GetUserReminderLogsAsync(string userId)
    {
        return await _reminderLogs
            .Find(l => l.UserId == userId)
            .SortByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ReminderLog>> GetReminderLogsAsync(string? userId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var filterBuilder = Builders<ReminderLog>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrEmpty(userId))
        {
            filter = filterBuilder.And(filter, filterBuilder.Eq(l => l.UserId, userId));
        }

        if (startDate.HasValue)
        {
            filter = filterBuilder.And(filter, filterBuilder.Gte(l => l.Timestamp, startDate.Value));
        }

        if (endDate.HasValue)
        {
            filter = filterBuilder.And(filter, filterBuilder.Lte(l => l.Timestamp, endDate.Value));
        }

        return await _reminderLogs
            .Find(filter)
            .SortByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    // MARK: - Streak Management
    public async Task<UserStreak?> GetUserStreakAsync(string userId)
    {
        return await _userStreaks.Find(s => s.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task UpdateUserStreakAsync(string userId)
    {
        var streak = await GetUserStreakAsync(userId);
        var today = DateTime.UtcNow.Date;

        if (streak == null)
        {
            streak = new UserStreak
            {
                UserId = userId,
                CurrentStreak = 1,
                LongestStreak = 1,
                LastCompletionDate = today,
                TotalCompletions = 1
            };
            await _userStreaks.InsertOneAsync(streak);
        }
        else
        {
            var lastDate = streak.LastCompletionDate?.Date;

            if (lastDate == null || lastDate == today.AddDays(-1))
            {
                // Continue streak
                streak.CurrentStreak++;
                streak.LongestStreak = Math.Max(streak.LongestStreak, streak.CurrentStreak);
            }
            else if (lastDate != today)
            {
                // Reset streak
                streak.CurrentStreak = 1;
            }

            streak.LastCompletionDate = today;
            streak.TotalCompletions++;

            await _userStreaks.ReplaceOneAsync(s => s.Id == streak.Id, streak);
        }
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int limit = 10)
    {
        try
        {
            // Get all user streaks sorted by performance
            var userStreaks = await _userStreaks
                .Find(Builders<UserStreak>.Filter.Empty)
                .SortByDescending(s => s.CurrentStreak)
                .ThenByDescending(s => s.TotalCompletions)
                .Limit(limit)
                .ToListAsync();

            var leaderboard = new List<LeaderboardEntry>();

            // For each streak, get the user information
            for (int i = 0; i < userStreaks.Count; i++)
            {
                var streak = userStreaks[i];
                var user = await _users.Find(u => u.Id == streak.UserId).FirstOrDefaultAsync();
                
                if (user != null)
                {
                    leaderboard.Add(new LeaderboardEntry
                    {
                        UserId = streak.UserId,
                        Username = user.Username,
                        CurrentStreak = streak.CurrentStreak,
                        TotalCompletions = streak.TotalCompletions,
                        Rank = i + 1
                    });
                }
            }

            return leaderboard;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLeaderboardAsync: {ex.Message}");
            throw;
        }
    }
} 