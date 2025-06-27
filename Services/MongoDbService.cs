using MongoDB.Bson;
using MongoDB.Driver;
using server.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace server.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IMongoCollection<Todo> _todosCollection;
    private readonly IMongoCollection<MemorizationPlan> _memorizationPlansCollection;
    private readonly IMongoCollection<Reminder> _remindersCollection;
    private readonly IMongoCollection<ReminderInteraction> _reminderInteractionsCollection;

    public MongoDbService(IConfiguration config)
    {
        var settings = MongoClientSettings.FromConnectionString(config["MongoDB:ConnectionString"]);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);

        var client = new MongoClient(settings);
        _database = client.GetDatabase(config["MongoDB:DatabaseName"]);

        _usersCollection = _database.GetCollection<User>(config["MongoDB:UsersCollectionName"]);
        _todosCollection = _database.GetCollection<Todo>(config["MongoDB:TodosCollectionName"]);
        _memorizationPlansCollection = _database.GetCollection<MemorizationPlan>(config["MongoDB:MemorizationPlansCollectionName"] ?? "memorizationPlans");
        _remindersCollection = _database.GetCollection<Reminder>(config["MongoDB:RemindersCollectionName"] ?? "reminders");
        _reminderInteractionsCollection = _database.GetCollection<ReminderInteraction>(config["MongoDB:ReminderInteractionsCollectionName"] ?? "reminderInteractions");
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            var result = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Users
    public async Task<List<User>> GetAllUsersAsync() =>
        await _usersCollection.Find(_ => true).ToListAsync();

    public async Task<User?> GetUserByIdAsync(string id) =>
        await _usersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        
    public async Task<User?> GetUserByEmailAsync(string email) =>
        await _usersCollection.Find(x => x.Email == email).FirstOrDefaultAsync();

    public async Task CreateUserAsync(User user) =>
        await _usersCollection.InsertOneAsync(user);

    public async Task UpdateUserAsync(string id, User user) =>
        await _usersCollection.ReplaceOneAsync(x => x.Id == id, user);

    public async Task DeleteUserAsync(string id) =>
        await _usersCollection.DeleteOneAsync(x => x.Id == id);
        
    // Todos
    public async Task<List<Todo>> GetAllTodosAsync() 
    {
        var todos = await _todosCollection.Find(_ => true).ToListAsync();
        var userIds = todos.Select(t => t.UserId).Distinct().ToList();
        
        // Get all users involved in these todos in a single query
        var users = await _usersCollection
            .Find(u => userIds.Contains(u.Id))
            .ToListAsync();
        
        // Create a lookup dictionary for faster access
        var userLookup = users.ToDictionary(u => u.Id, u => u);

        // Add username to each todo
        foreach (var todo in todos)
        {
            if (userLookup.TryGetValue(todo.UserId, out var user))
            {
                todo.Username = user.Username ?? user.Email;
            }
            else
            {
                todo.Username = "Unknown User";
            }
        }
        
        return todos;
    }
        
    public async Task<List<Todo>> GetTodosByUserIdAsync(string userId)
    {
        var todos = await _todosCollection.Find(x => x.UserId == userId).ToListAsync();
        
        // Get the user
        var user = await GetUserByIdAsync(userId);
        
        // Set username on all todos
        if (user != null)
        {
            foreach (var todo in todos)
            {
                todo.Username = user.Username ?? user.Email;
            }
        }
        
        return todos;
    }

    public async Task<Todo?> GetTodoByIdAsync(string id) =>
        await _todosCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreateTodoAsync(Todo todo) =>
        await _todosCollection.InsertOneAsync(todo);

    public async Task UpdateTodoAsync(string id, Todo todo) =>
        await _todosCollection.ReplaceOneAsync(x => x.Id == id, todo);
        
    public async Task UpdateTodoCompletionAsync(string id, bool isCompleted) =>
        await _todosCollection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Todo>.Update
                .Set(x => x.IsCompleted, isCompleted)
                .Set(x => x.UpdatedAt, DateTime.UtcNow));

    public async Task DeleteTodoAsync(string id) =>
        await _todosCollection.DeleteOneAsync(x => x.Id == id);
        

        
    // Memorization Plans
    public async Task<MemorizationPlan?> GetMemorizationPlanByUserIdAsync(string userId) =>
        await _memorizationPlansCollection.Find(x => x.UserId == userId).FirstOrDefaultAsync();
        
    public async Task<MemorizationPlan?> GetMemorizationPlanByIdAsync(string id) =>
        await _memorizationPlansCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        
    public async Task CreateMemorizationPlanAsync(MemorizationPlan plan)
    {
        // Set timestamp
        plan.CreatedAt = DateTime.UtcNow;
        plan.UpdatedAt = DateTime.UtcNow;
        
        // Remove any existing plan for this user
        await _memorizationPlansCollection.DeleteManyAsync(x => x.UserId == plan.UserId);
        
        // Insert the new plan
        await _memorizationPlansCollection.InsertOneAsync(plan);
    }
    
    public async Task UpdateMemorizationPlanAsync(string id, MemorizationPlan plan)
    {
        // Update timestamp
        plan.UpdatedAt = DateTime.UtcNow;
        
        await _memorizationPlansCollection.ReplaceOneAsync(x => x.Id == id, plan);
    }
    
    public async Task UpdateMemorizationPlanProgressAsync(string id, Progress progress)
    {
        await _memorizationPlansCollection.UpdateOneAsync(
            x => x.Id == id,
            Builders<MemorizationPlan>.Update
                .Set(x => x.Progress, progress)
                .Set(x => x.UpdatedAt, DateTime.UtcNow));
    }
    
    public async Task DeleteMemorizationPlanAsync(string id) =>
        await _memorizationPlansCollection.DeleteOneAsync(x => x.Id == id);
        
    public async Task DeleteMemorizationPlansByUserIdAsync(string userId) =>
        await _memorizationPlansCollection.DeleteManyAsync(x => x.UserId == userId);
        
    // Reminders
    public async Task<List<Reminder>> GetRemindersByUserIdAsync(string userId) =>
        await _remindersCollection.Find(x => x.UserId == userId).ToListAsync();
        
    public async Task<List<Reminder>> GetActiveRemindersByUserIdAsync(string userId) =>
        await _remindersCollection.Find(x => x.UserId == userId && x.IsActive && (x.PausedUntil == null || x.PausedUntil <= DateTime.UtcNow)).ToListAsync();
        
    public async Task<List<Reminder>> GetDueRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var currentTime = now.TimeOfDay;
        
        return await _remindersCollection.Find(x => 
            x.IsActive && 
            !x.IsCompleted &&
            (x.PausedUntil == null || x.PausedUntil <= now) &&
            (x.SnoozedUntil == null || x.SnoozedUntil <= now) &&
            x.StartTime <= currentTime &&
            x.EndTime >= currentTime &&
            (x.LastReminderDate != today || x.TimesRemindedToday < x.MaxRemindersPerDay)
        ).ToListAsync();
    }
        
    public async Task<Reminder?> GetReminderByIdAsync(string id) =>
        await _remindersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        
    public async Task CreateReminderAsync(Reminder reminder)
    {
        reminder.CreatedAt = DateTime.UtcNow;
        reminder.UpdatedAt = DateTime.UtcNow;
        await _remindersCollection.InsertOneAsync(reminder);
    }
    
    public async Task UpdateReminderAsync(string id, Reminder reminder)
    {
        reminder.UpdatedAt = DateTime.UtcNow;
        await _remindersCollection.ReplaceOneAsync(x => x.Id == id, reminder);
    }
    
    public async Task UpdateReminderInteractionAsync(string id, ReminderAction action, string? notes = null)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        
        // First, get the current reminder state to avoid race conditions
        var reminder = await GetReminderByIdAsync(id);
        if (reminder == null)
        {
            throw new InvalidOperationException($"Reminder with ID {id} not found");
        }
        
        var updateBuilder = Builders<Reminder>.Update
            .Set(x => x.LastInteractionType, action.ToString())
            .Set(x => x.LastInteractionTime, now)
            .Set(x => x.UpdatedAt, now);
            
        // Reset daily counter if it's a new day
        if (reminder.LastReminderDate != today)
        {
            updateBuilder = updateBuilder.Set(x => x.TimesRemindedToday, 0);
        }
        
        switch (action)
        {
            case ReminderAction.Completed:
                updateBuilder = updateBuilder
                    .Set(x => x.IsCompleted, true)
                    .Inc(x => x.CompletedCount, 1)
                    .Set(x => x.SnoozedUntil, null);
                break;
                
            case ReminderAction.Later:
                updateBuilder = updateBuilder
                    .Set(x => x.SnoozedUntil, now.AddMinutes(30))
                    .Inc(x => x.SnoozedCount, 1);
                break;
                
            case ReminderAction.Busy:
                // Smart snooze logic - 5-30 minutes based on time within window
                var snoozeMinutes = CalculateSmartSnooze(reminder, now);
                updateBuilder = updateBuilder
                    .Set(x => x.SnoozedUntil, now.AddMinutes(snoozeMinutes))
                    .Inc(x => x.SnoozedCount, 1);
                break;
                
            case ReminderAction.Skip:
                updateBuilder = updateBuilder
                    .Set(x => x.PausedUntil, today.AddDays(1).AddHours(11)) // Resume tomorrow at 11:00
                    .Inc(x => x.SkippedCount, 1);
                break;
        }
        
        var result = await _remindersCollection.UpdateOneAsync(x => x.Id == id, updateBuilder);
        
        // Verify the update was successful
        if (result.ModifiedCount == 0)
        {
            throw new InvalidOperationException($"Failed to update reminder {id}. Reminder may have been modified by another operation.");
        }
    }
    
    private int CalculateSmartSnooze(Reminder? reminder, DateTime now)
    {
        if (reminder == null) return 15; // Default fallback
        
        var currentTime = now.TimeOfDay;
        var windowDuration = reminder.EndTime - reminder.StartTime;
        
        // Handle edge case where start and end time are the same
        if (windowDuration.TotalMinutes <= 0)
        {
            return 15; // Default snooze for zero-duration windows
        }
        
        var timeIntoWindow = currentTime - reminder.StartTime;
        
        // Handle case where current time is before the start time
        if (timeIntoWindow.TotalMinutes < 0)
        {
            return 30; // Longer snooze if we're before the window
        }
        
        // Handle case where current time is after the end time
        if (timeIntoWindow.TotalMinutes > windowDuration.TotalMinutes)
        {
            return 5; // Shorter snooze if we're past the window
        }
        
        var progressInWindow = timeIntoWindow.TotalMinutes / windowDuration.TotalMinutes;
        
        // Early in window: longer snooze (30 min), later in window: shorter snooze (5 min)
        var snoozeMinutes = (int)(30 - (progressInWindow * 25));
        return Math.Max(5, Math.Min(30, snoozeMinutes));
    }
    
    public async Task IncrementReminderCountAsync(string id)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        
        await _remindersCollection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Reminder>.Update
                .Inc(x => x.TimesRemindedToday, 1)
                .Inc(x => x.TotalTimesReminded, 1)
                .Set(x => x.LastReminderDate, today)
                .Set(x => x.UpdatedAt, now));
    }
    
    public async Task DeleteReminderAsync(string id) =>
        await _remindersCollection.DeleteOneAsync(x => x.Id == id);
        
    public async Task ResetDailyReminderCompletionsAsync()
    {
        var today = DateTime.UtcNow.Date;
        await _remindersCollection.UpdateManyAsync(
            x => x.LastReminderDate != today,
            Builders<Reminder>.Update
                .Set(x => x.IsCompleted, false)
                .Set(x => x.TimesRemindedToday, 0)
                .Set(x => x.UpdatedAt, DateTime.UtcNow));
    }
    
    // Reminder Interactions
    public async Task CreateReminderInteractionAsync(ReminderInteraction interaction)
    {
        interaction.Timestamp = DateTime.UtcNow;
        await _reminderInteractionsCollection.InsertOneAsync(interaction);
    }
    
    public async Task<List<ReminderInteraction>> GetReminderInteractionsByUserIdAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var filter = Builders<ReminderInteraction>.Filter.Eq(x => x.UserId, userId);
        
        if (startDate.HasValue)
        {
            filter = filter & Builders<ReminderInteraction>.Filter.Gte(x => x.Timestamp, startDate.Value);
        }
        
        if (endDate.HasValue)
        {
            filter = filter & Builders<ReminderInteraction>.Filter.Lte(x => x.Timestamp, endDate.Value);
        }
        
        return await _reminderInteractionsCollection.Find(filter).SortByDescending(x => x.Timestamp).ToListAsync();
    }
    
    public async Task<ReminderStats> GetReminderStatsAsync(string userId)
    {
        var reminders = await GetRemindersByUserIdAsync(userId);
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        
        var interactions = await GetReminderInteractionsByUserIdAsync(userId, monthStart);
        
        var completedToday = interactions.Count(x => 
            x.Action == ReminderAction.Completed && 
            x.Timestamp.Date == today);
            
        var completedThisWeek = interactions.Count(x => 
            x.Action == ReminderAction.Completed && 
            x.Timestamp.Date >= weekStart);
            
        var completedThisMonth = interactions.Count(x => 
            x.Action == ReminderAction.Completed && 
            x.Timestamp.Date >= monthStart);
        
        // Calculate consistency rate (days with at least one completion in the last 30 days)
        var last30Days = today.AddDays(-30);
        var completionDays = interactions
            .Where(x => x.Action == ReminderAction.Completed && x.Timestamp.Date >= last30Days)
            .Select(x => x.Timestamp.Date)
            .Distinct()
            .Count();
        var consistencyRate = (completionDays / 30.0) * 100;
        
        // Calculate streaks
        var currentStreak = CalculateCurrentStreak(interactions, today);
        var longestStreak = CalculateLongestStreak(interactions);
        
        return new ReminderStats
        {
            TotalReminders = reminders.Count,
            CompletedToday = completedToday,
            CompletedThisWeek = completedThisWeek,
            CompletedThisMonth = completedThisMonth,
            ConsistencyRate = Math.Round(consistencyRate, 1),
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak
        };
    }
    
    private int CalculateCurrentStreak(List<ReminderInteraction> interactions, DateTime today)
    {
        var streak = 0;
        var currentDate = today;
        
        while (true)
        {
            var hasCompletion = interactions.Any(x => 
                x.Action == ReminderAction.Completed && 
                x.Timestamp.Date == currentDate);
                
            if (hasCompletion)
            {
                streak++;
                currentDate = currentDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }
        
        return streak;
    }
    
    private int CalculateLongestStreak(List<ReminderInteraction> interactions)
    {
        if (!interactions.Any()) return 0;
        
        var completionDays = interactions
            .Where(x => x.Action == ReminderAction.Completed)
            .Select(x => x.Timestamp.Date)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
            
        if (!completionDays.Any()) return 0;
        
        var longestStreak = 1;
        var currentStreak = 1;
        
        for (int i = 1; i < completionDays.Count; i++)
        {
            if (completionDays[i] == completionDays[i - 1].AddDays(1))
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else
            {
                currentStreak = 1;
            }
        }
        
        return longestStreak;
    }
    
    public async Task SyncOfflineInteractionsAsync(string userId, List<OfflineInteraction> interactions)
    {
        foreach (var interaction in interactions)
        {
            // Create the interaction record
            var reminderInteraction = new ReminderInteraction
            {
                ReminderId = interaction.ReminderId,
                UserId = userId,
                Action = interaction.Action,
                Timestamp = interaction.Timestamp,
                Notes = interaction.Notes,
                LocalId = interaction.LocalId,
                IsSynced = true
            };
            
            await CreateReminderInteractionAsync(reminderInteraction);
            
            // Update the reminder based on the action
            await UpdateReminderInteractionAsync(interaction.ReminderId, interaction.Action, interaction.Notes);
        }
    }
}