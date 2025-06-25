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
    private readonly IMongoCollection<Reminder> _remindersCollection;
    private readonly IMongoCollection<MemorizationPlan> _memorizationPlansCollection;

    public MongoDbService(IConfiguration config)
    {
        var settings = MongoClientSettings.FromConnectionString(config["MongoDB:ConnectionString"]);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);

        var client = new MongoClient(settings);
        _database = client.GetDatabase(config["MongoDB:DatabaseName"]);

        _usersCollection = _database.GetCollection<User>(config["MongoDB:UsersCollectionName"]);
        _todosCollection = _database.GetCollection<Todo>(config["MongoDB:TodosCollectionName"]);
        _remindersCollection = _database.GetCollection<Reminder>(config["MongoDB:RemindersCollectionName"] ?? "Reminders");
        _memorizationPlansCollection = _database.GetCollection<MemorizationPlan>(config["MongoDB:MemorizationPlansCollectionName"] ?? "memorizationPlans");
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
        
    // Reminders
    public async Task<List<Reminder>> GetAllRemindersAsync() 
    {
        var reminders = await _remindersCollection.Find(_ => true).ToListAsync();
        var userIds = reminders.Select(r => r.UserId).Distinct().ToList();
        
        // Get all users involved in these reminders in a single query
        var users = await _usersCollection
            .Find(u => userIds.Contains(u.Id))
            .ToListAsync();
        
        // Create a lookup dictionary for faster access
        var userLookup = users.ToDictionary(u => u.Id, u => u);

        // Add username to each reminder
        foreach (var reminder in reminders)
        {
            if (userLookup.TryGetValue(reminder.UserId, out var user))
            {
                reminder.Username = user.Username ?? user.Email;
            }
            else
            {
                reminder.Username = "Unknown User";
            }
        }
        
        return reminders;
    }
        
    public async Task<List<Reminder>> GetRemindersByUserIdAsync(string userId)
    {
        var reminders = await _remindersCollection.Find(x => x.UserId == userId).ToListAsync();
        
        // Get the user
        var user = await GetUserByIdAsync(userId);
        
        // Set username on all reminders
        if (user != null)
        {
            foreach (var reminder in reminders)
            {
                reminder.Username = user.Username ?? user.Email;
            }
        }
        
        return reminders;
    }
    
    public async Task<List<Reminder>> GetActiveRemindersByUserIdAsync(string userId) =>
        await _remindersCollection.Find(x => x.UserId == userId && x.IsActive == true).ToListAsync();
    
    public async Task<List<Reminder>> GetCompletedRemindersByUserIdAsync(string userId) =>
        await _remindersCollection.Find(x => x.UserId == userId && x.IsCompleted == true).ToListAsync();
    
    public async Task<List<Reminder>> GetHighPriorityRemindersByUserIdAsync(string userId) =>
        await _remindersCollection.Find(x => x.UserId == userId && x.Priority == 2 && x.IsActive == true).ToListAsync();
    
    public async Task<List<Reminder>> GetTodayRemindersByUserIdAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        return await _remindersCollection.Find(x => 
            x.UserId == userId && 
            x.IsActive == true &&
            (x.DueDate >= today && x.DueDate < tomorrow)).ToListAsync();
    }

    public async Task<Reminder?> GetReminderByIdAsync(string id) =>
        await _remindersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreateReminderAsync(Reminder reminder) =>
        await _remindersCollection.InsertOneAsync(reminder);

    public async Task UpdateReminderAsync(string id, Reminder reminder) =>
        await _remindersCollection.ReplaceOneAsync(x => x.Id == id, reminder);
        
    public async Task UpdateReminderCompletionAsync(string id, bool isCompleted) =>
        await _remindersCollection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Reminder>.Update
                .Set(x => x.IsCompleted, isCompleted)
                .Set(x => x.UpdatedAt, DateTime.UtcNow));
                
    public async Task UpdateReminderActiveStatusAsync(string id, bool isActive) =>
        await _remindersCollection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Reminder>.Update
                .Set(x => x.IsActive, isActive)
                .Set(x => x.UpdatedAt, DateTime.UtcNow));
                
    public async Task IncrementReminderCountAsync(string id) =>
        await _remindersCollection.UpdateOneAsync(
            x => x.Id == id,
            Builders<Reminder>.Update
                .Inc(x => x.ReminderCount, 1)
                .Set(x => x.LastReminderSent, DateTime.UtcNow)
                .Set(x => x.UpdatedAt, DateTime.UtcNow));

    public async Task DeleteReminderAsync(string id) =>
        await _remindersCollection.DeleteOneAsync(x => x.Id == id);
        
    public async Task<ReminderStatsDto> GetReminderStatsAsync(string userId)
    {
        var totalReminders = await _remindersCollection.CountDocumentsAsync(x => x.UserId == userId);
        var completedReminders = await _remindersCollection.CountDocumentsAsync(x => x.UserId == userId && x.IsCompleted == true);
        var activeReminders = await _remindersCollection.CountDocumentsAsync(x => x.UserId == userId && x.IsActive == true);
        var highPriorityReminders = await _remindersCollection.CountDocumentsAsync(x => x.UserId == userId && x.Priority == 2 && x.IsActive == true);
        
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var todayReminders = await _remindersCollection.CountDocumentsAsync(x => 
            x.UserId == userId && 
            x.IsActive == true &&
            (x.DueDate >= today && x.DueDate < tomorrow));
        
        var completionRate = totalReminders > 0 ? (double)completedReminders / totalReminders * 100 : 0;
        
        return new ReminderStatsDto
        {
            TotalReminders = (int)totalReminders,
            CompletedReminders = (int)completedReminders,
            ActiveReminders = (int)activeReminders,
            HighPriorityReminders = (int)highPriorityReminders,
            TodayReminders = (int)todayReminders,
            CompletionRate = Math.Round(completionRate, 2)
        };
    }
        
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
}