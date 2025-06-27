using Microsoft.Extensions.Configuration;
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

    public MongoDbService(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("MongoDB");
        var mongoUrl = MongoUrl.Create(connectionString);
        var mongoClient = new MongoClient(mongoUrl);
        _database = mongoClient.GetDatabase(mongoUrl.DatabaseName);

        // Initialize collections
        _usersCollection = _database.GetCollection<User>(config["MongoDB:UsersCollectionName"]);
        _todosCollection = _database.GetCollection<Todo>(config["MongoDB:TodosCollectionName"]);
    }

    // MongoDB Connection Test
    public async Task<bool> PingAsync()
    {
        try
        {
            await _database.RunCommandAsync((Command<MongoDB.Bson.BsonDocument>)"{ping:1}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    // User operations
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

    public async Task UpdateUserAsync(string id, UpdateDefinition<User> update) =>
        await _usersCollection.UpdateOneAsync(x => x.Id == id, update);

    public async Task DeleteUserAsync(string id) =>
        await _usersCollection.DeleteOneAsync(x => x.Id == id);

    // Token management methods
    public async Task StoreRefreshTokenAsync(string userId, string refreshToken, DateTime expiryTime)
    {
        var update = Builders<User>.Update
            .Set(x => x.RefreshToken, refreshToken)
            .Set(x => x.RefreshTokenExpiryTime, expiryTime);

        await _usersCollection.UpdateOneAsync(x => x.Id == userId, update);
    }

    public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        return await _usersCollection.Find(x => 
            x.RefreshToken == refreshToken && 
            x.RefreshTokenExpiryTime > DateTime.UtcNow
        ).FirstOrDefaultAsync();
    }

    public async Task RevokeRefreshTokenAsync(string userId)
    {
        var update = Builders<User>.Update
            .Unset(x => x.RefreshToken)
            .Unset(x => x.RefreshTokenExpiryTime);

        await _usersCollection.UpdateOneAsync(x => x.Id == userId, update);
    }

    public async Task RevokeAllRefreshTokensForUserAsync(string userId)
    {
        await RevokeRefreshTokenAsync(userId);
    }

    // Todo operations
    public async Task<List<Todo>> GetAllTodosAsync() 
    {
        try 
        {
            var todos = await _todosCollection.Find(_ => true).ToListAsync();
            
            foreach (var todo in todos)
            {
                Console.WriteLine($"Todo ID from DB: '{todo.Id}' (Length: {todo.Id?.Length})");
                if (string.IsNullOrEmpty(todo.Id))
                {
                    Console.WriteLine("Warning: Found todo with null or empty ID");
                }
            }
            
            return todos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAllTodosAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Todo>> GetTodosByUserIdAsync(string userId)
    {
        try 
        {
            var todos = await _todosCollection.Find(x => x.UserId == userId).ToListAsync();
            
            foreach (var todo in todos)
            {
                Console.WriteLine($"Todo for user {userId}: ID='{todo.Id}', Title='{todo.Title}'");
            }
            
            return todos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetTodosByUserIdAsync for user {userId}: {ex.Message}");
            throw;
        }
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
            Builders<Todo>.Update.Set(x => x.IsCompleted, isCompleted)
        );

    public async Task DeleteTodoAsync(string id) =>
        await _todosCollection.DeleteOneAsync(x => x.Id == id);
}