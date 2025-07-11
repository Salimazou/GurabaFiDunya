using MongoDB.Driver;
using GurabaFiDunya.Models;

namespace GurabaFiDunya.Services;

public class DatabaseService
{
    private readonly IMongoDatabase _database;
    
    public DatabaseService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDB") ?? 
                             throw new InvalidOperationException("MongoDB connection string not found");
        
        var mongoClient = new MongoClient(connectionString);
        var databaseName = configuration["MongoDB:DatabaseName"] ?? "IslamApp";
        _database = mongoClient.GetDatabase(databaseName);
    }
    
    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Reminder> Reminders => _database.GetCollection<Reminder>("reminders");
    public IMongoCollection<ReminderLog> ReminderLogs => _database.GetCollection<ReminderLog>("reminderLogs");
    
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await _database.RunCommandAsync((Command<object>)"{ping:1}");
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task InitializeIndexesAsync()
    {
        // Create indexes for better performance
        var userIndexKeys = Builders<User>.IndexKeys.Ascending(u => u.Email);
        var userIndexOptions = new CreateIndexOptions { Unique = true };
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(userIndexKeys, userIndexOptions));
        
        var reminderIndexKeys = Builders<Reminder>.IndexKeys.Ascending(r => r.UserId);
        await Reminders.Indexes.CreateOneAsync(new CreateIndexModel<Reminder>(reminderIndexKeys));
        
        var reminderLogIndexKeys = Builders<ReminderLog>.IndexKeys
            .Ascending(rl => rl.UserId)
            .Ascending(rl => rl.Date);
        await ReminderLogs.Indexes.CreateOneAsync(new CreateIndexModel<ReminderLog>(reminderLogIndexKeys));
    }
} 