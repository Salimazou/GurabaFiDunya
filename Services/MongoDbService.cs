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
    private readonly IMongoCollection<FavoriteReciter> _favoriteRecitersCollection;
    private readonly IMongoCollection<ReminderCompletion> _reminderCompletionsCollection;
    private readonly ILogger<MongoDbService> _logger;

    public MongoDbService(IConfiguration config, ILogger<MongoDbService> logger)
    {
        _logger = logger;
        
        var settings = MongoClientSettings.FromConnectionString(config["MongoDB:ConnectionString"]);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);

        var client = new MongoClient(settings);
        _database = client.GetDatabase(config["MongoDB:DatabaseName"]);

        // Initialize collections
        _usersCollection = _database.GetCollection<User>(config["MongoDB:UsersCollectionName"]);
        _todosCollection = _database.GetCollection<Todo>(config["MongoDB:TodosCollectionName"]);
        _favoriteRecitersCollection = _database.GetCollection<FavoriteReciter>(config["MongoDB:FavoriteRecitersCollectionName"]);
        _reminderCompletionsCollection = _database.GetCollection<ReminderCompletion>(config["MongoDB:ReminderCompletionsCollectionName"]);
        
        // Create indexes
        CreateIndexesAsync().ConfigureAwait(false);
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

    public async Task<UserDto?> GetUserDtoWithFavoritesAsync(string id)
    {
        try
        {
            var user = await GetUserByIdAsync(id);
            if (user == null) return null;

            var favoriteReciters = await GetUserFavoriteRecitersAsync(id);
            var favoriteReciterIds = favoriteReciters.Select(fr => fr.ReciterId).ToList();

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FavoriteReciters = favoriteReciterIds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user DTO with favorites for user {UserId}", id);
            throw;
        }
    }

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
            _logger.LogInformation("Retrieved {TodoCount} todos from database", todos.Count);
            return todos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all todos");
            throw;
        }
    }

    public async Task<List<Todo>> GetTodosByUserIdAsync(string userId)
    {
        try
        {
            var todos = await _todosCollection.Find(x => x.UserId == userId).ToListAsync();
            _logger.LogInformation("Retrieved {TodoCount} todos for user {UserId}", todos.Count, userId);
            return todos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving todos for user {UserId}", userId);
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

    // Favorite Reciters operations
    public async Task<List<FavoriteReciter>> GetUserFavoriteRecitersAsync(string userId)
    {
        try
        {
            return await _favoriteRecitersCollection
                .Find(x => x.UserId == userId && x.IsActive)
                .SortBy(x => x.Order)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite reciters for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> AddFavoriteReciterAsync(string userId, string reciterId)
    {
        try
        {
            var nextOrder = await GetNextOrderForUserAsync(userId);
            
            var favoriteReciter = new FavoriteReciter
            {
                UserId = userId,
                ReciterId = reciterId,
                Order = nextOrder
            };
            
            await _favoriteRecitersCollection.InsertOneAsync(favoriteReciter);
            _logger.LogInformation("Added favorite reciter {ReciterId} for user {UserId}", reciterId, userId);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning("Reciter {ReciterId} is already a favorite for user {UserId}", reciterId, userId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding favorite reciter {ReciterId} for user {UserId}", reciterId, userId);
            throw;
        }
    }

    public async Task<bool> RemoveFavoriteReciterAsync(string userId, string reciterId)
    {
        try
        {
            var result = await _favoriteRecitersCollection.DeleteOneAsync(
                x => x.UserId == userId && x.ReciterId == reciterId
            );
            
            if (result.DeletedCount > 0)
            {
                _logger.LogInformation("Removed favorite reciter {ReciterId} for user {UserId}", reciterId, userId);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite reciter {ReciterId} for user {UserId}", reciterId, userId);
            throw;
        }
    }

    public async Task<bool> IsFavoriteReciterAsync(string userId, string reciterId)
    {
        try
        {
            var count = await _favoriteRecitersCollection.CountDocumentsAsync(
                x => x.UserId == userId && x.ReciterId == reciterId && x.IsActive
            );
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if reciter {ReciterId} is favorite for user {UserId}", reciterId, userId);
            throw;
        }
    }

    public async Task<bool> ReorderFavoriteRecitersAsync(string userId, List<string> reciterIdsInOrder)
    {
        try
        {
            var bulkOps = new List<WriteModel<FavoriteReciter>>();
            
            for (int i = 0; i < reciterIdsInOrder.Count; i++)
            {
                var filter = Builders<FavoriteReciter>.Filter.And(
                    Builders<FavoriteReciter>.Filter.Eq(x => x.UserId, userId),
                    Builders<FavoriteReciter>.Filter.Eq(x => x.ReciterId, reciterIdsInOrder[i])
                );
                
                var update = Builders<FavoriteReciter>.Update.Set(x => x.Order, i);
                bulkOps.Add(new UpdateOneModel<FavoriteReciter>(filter, update));
            }
            
            if (bulkOps.Count > 0)
            {
                var result = await _favoriteRecitersCollection.BulkWriteAsync(bulkOps);
                _logger.LogInformation("Reordered {Count} favorite reciters for user {UserId}", result.ModifiedCount, userId);
                return result.ModifiedCount > 0;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering favorite reciters for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetUserFavoriteRecitersCountAsync(string userId)
    {
        try
        {
            return (int)await _favoriteRecitersCollection.CountDocumentsAsync(
                x => x.UserId == userId && x.IsActive
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite reciters count for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<FavoriteReciterStatsDto>> GetMostPopularRecitersAsync(int limit = 10)
    {
        try
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("isActive", true)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$reciterId" },
                    { "count", new BsonDocument("$sum", 1) },
                    { "totalListens", new BsonDocument("$sum", "$listenCount") }
                }),
                new BsonDocument("$sort", new BsonDocument("count", -1)),
                new BsonDocument("$limit", limit)
            };
            
            var results = await _favoriteRecitersCollection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync();
                
            return results.Select(doc => new FavoriteReciterStatsDto
            {
                ReciterId = doc["_id"].AsString,
                FavoriteCount = doc["count"].AsInt32,
                TotalListens = doc["totalListens"].AsInt32
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular reciters stats");
            throw;
        }
    }

    public async Task IncrementListenCountAsync(string userId, string reciterId)
    {
        try
        {
            var filter = Builders<FavoriteReciter>.Filter.And(
                Builders<FavoriteReciter>.Filter.Eq(x => x.UserId, userId),
                Builders<FavoriteReciter>.Filter.Eq(x => x.ReciterId, reciterId)
            );
            
            var update = Builders<FavoriteReciter>.Update
                .Inc(x => x.ListenCount, 1)
                .Set(x => x.LastListenedAt, DateTime.UtcNow);
                
            await _favoriteRecitersCollection.UpdateOneAsync(filter, update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing listen count for reciter {ReciterId}, user {UserId}", reciterId, userId);
            // Don't throw - this is not critical
        }
    }

    // Reminder Completion operations
    public async Task<bool> SyncReminderCompletionsAsync(string userId, List<CreateReminderCompletionRequest> completions)
    {
        try
        {
            if (completions == null || !completions.Any())
            {
                _logger.LogWarning("No completions provided for sync for user {UserId}", userId);
                return true; // Nothing to sync
            }

            var bulkOperations = new List<WriteModel<ReminderCompletion>>();
            var processedCount = 0;
            
            foreach (var completion in completions)
            {
                try
                {
                    // Validate completion data
                    if (string.IsNullOrWhiteSpace(completion.ReminderId) || string.IsNullOrWhiteSpace(completion.ReminderTitle))
                    {
                        _logger.LogWarning("Invalid completion data: ReminderId={ReminderId}, ReminderTitle={ReminderTitle}", 
                            completion.ReminderId, completion.ReminderTitle);
                        continue;
                    }

                    // Normalize DeviceId - CRITICAL for consistent duplicate detection
                    var normalizedDeviceId = string.IsNullOrWhiteSpace(completion.DeviceId) ? "unknown" : completion.DeviceId.Trim();
                    
                    // Create the reminder completion object
                    var reminderCompletion = new ReminderCompletion
                    {
                        Id = null, // CRITICAL: Must be null for MongoDB auto-generation, NOT empty string
                        UserId = userId,
                        ReminderId = completion.ReminderId.Trim(),
                        ReminderTitle = completion.ReminderTitle.Trim(),
                        CompletedAt = completion.CompletedAt,
                        CompletionDate = completion.CompletionDate.Date, // Normalize to date only
                        DeviceId = normalizedDeviceId,
                        SyncedAt = DateTime.UtcNow
                    };
                    
                    // ATOMIC UPSERT OPERATION - Prevents race conditions
                    // Filter to identify unique completion (composite key)
                    var filter = Builders<ReminderCompletion>.Filter.And(
                        Builders<ReminderCompletion>.Filter.Eq(x => x.UserId, userId),
                        Builders<ReminderCompletion>.Filter.Eq(x => x.ReminderId, reminderCompletion.ReminderId),
                        Builders<ReminderCompletion>.Filter.Eq(x => x.CompletionDate, reminderCompletion.CompletionDate),
                        Builders<ReminderCompletion>.Filter.Eq(x => x.DeviceId, normalizedDeviceId)
                    );
                    
                    // Use ReplaceOneModel with upsert=true for atomic operation
                    var replaceOperation = new ReplaceOneModel<ReminderCompletion>(filter, reminderCompletion)
                    {
                        IsUpsert = true
                    };
                    
                    bulkOperations.Add(replaceOperation);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing individual completion for user {UserId}, reminder {ReminderId}, skipping", 
                        userId, completion?.ReminderId ?? "unknown");
                    continue;
                }
            }
            
            if (bulkOperations.Count > 0)
            {
                try
                {
                    var bulkWriteOptions = new BulkWriteOptions 
                    { 
                        IsOrdered = false, // Allow parallel processing for better performance
                        BypassDocumentValidation = false
                    };
                    
                    var result = await _reminderCompletionsCollection.BulkWriteAsync(bulkOperations, bulkWriteOptions);
                    
                    _logger.LogInformation("Synced {ProcessedCount} reminder completions for user {UserId}. " +
                        "Inserted: {InsertedCount}, Modified: {ModifiedCount}, Upserted: {UpsertedCount}", 
                        processedCount, userId, result.InsertedCount, result.ModifiedCount, result.Upserts.Count);
                }
                catch (MongoBulkWriteException ex)
                {
                    // Handle duplicate key errors gracefully - this can still happen during high concurrency
                    var duplicateErrors = ex.WriteErrors?.Where(e => e.Code == 11000).ToList() ?? new List<MongoBulkWriteError>();
                    var otherErrors = ex.WriteErrors?.Where(e => e.Code != 11000).ToList() ?? new List<MongoBulkWriteError>();
                    
                    if (duplicateErrors.Any())
                    {
                        _logger.LogInformation("Encountered {DuplicateCount} duplicate key errors during sync for user {UserId} - these are expected and handled gracefully", 
                            duplicateErrors.Count, userId);
                    }
                    
                    if (otherErrors.Any())
                    {
                        _logger.LogError("Encountered {ErrorCount} non-duplicate errors during sync for user {UserId}: {Errors}", 
                            otherErrors.Count, userId, string.Join("; ", otherErrors.Select(e => e.Message)));
                        throw; // Re-throw for serious errors
                    }
                    
                    // If only duplicate errors, log success with processed results
                    var result = ex.Result;
                    _logger.LogInformation("Synced {ProcessedCount} reminder completions for user {UserId} with {DuplicateCount} duplicates handled. " +
                        "Inserted: {InsertedCount}, Modified: {ModifiedCount}, Upserted: {UpsertedCount}", 
                        processedCount, userId, duplicateErrors.Count, result.InsertedCount, result.ModifiedCount, result.Upserts.Count);
                }
            }
            else
            {
                _logger.LogInformation("No valid completions to sync for user {UserId}", userId);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing reminder completions for user {UserId}. Details: {ExceptionMessage}", userId, ex.Message);
            throw;
        }
    }
    
    public async Task<List<ReminderCompletionDto>> GetUserReminderCompletionsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var filterBuilder = Builders<ReminderCompletion>.Filter;
            var filter = filterBuilder.Eq(x => x.UserId, userId);
            
            if (startDate.HasValue)
                filter = filterBuilder.And(filter, filterBuilder.Gte(x => x.CompletionDate, startDate.Value.Date));
                
            if (endDate.HasValue)
                filter = filterBuilder.And(filter, filterBuilder.Lte(x => x.CompletionDate, endDate.Value.Date));
            
            var completions = await _reminderCompletionsCollection
                .Find(filter)
                .SortByDescending(x => x.CompletionDate)
                .ToListAsync();
            
            return completions.Select(c => new ReminderCompletionDto
            {
                Id = c.Id,
                ReminderId = c.ReminderId,
                ReminderTitle = c.ReminderTitle,
                CompletedAt = c.CompletedAt,
                CompletionDate = c.CompletionDate,
                SyncedAt = c.SyncedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reminder completions for user {UserId}", userId);
            throw;
        }
    }
    
    public async Task<UserStreakInfo> GetUserStreakInfoAsync(string userId)
    {
        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for streak info", userId);
                // Return default streak info instead of throwing
                return new UserStreakInfo
                {
                    UserId = userId,
                    Username = "Unknown User",
                    CurrentStreak = 0,
                    LongestStreak = 0,
                    TotalCompletions = 0,
                    LastCompletionDate = null,
                    RecentReminders = new List<string>()
                };
            }

            var completions = await _reminderCompletionsCollection
                .Find(x => x.UserId == userId)
                .SortByDescending(x => x.CompletionDate)
                .ToListAsync();

            var streakInfo = new UserStreakInfo
            {
                UserId = userId,
                Username = user.Username,
                TotalCompletions = completions.Count,
                CurrentStreak = 0,
                LongestStreak = 0,
                LastCompletionDate = null,
                RecentReminders = new List<string>()
            };

            if (completions.Count == 0)
            {
                _logger.LogInformation("No reminder completions found for user {UserId}", userId);
                return streakInfo;
            }

            // Calculate current streak
            var today = DateTime.UtcNow.Date;
            var currentStreak = 0;
            var longestStreak = 0;
            var tempStreak = 0;

            // Group by date to handle multiple completions per day
            var completionsByDate = completions
                .GroupBy(c => c.CompletionDate.Date)
                .OrderByDescending(g => g.Key)
                .ToList();

            streakInfo.LastCompletionDate = completionsByDate.FirstOrDefault()?.Key;

            // Calculate current streak (from today backwards)
            var checkDate = today;
            foreach (var dateGroup in completionsByDate)
            {
                if (dateGroup.Key == checkDate || dateGroup.Key == checkDate.AddDays(-1))
                {
                    currentStreak++;
                    checkDate = dateGroup.Key.AddDays(-1);
                }
                else
                {
                    break;
                }
            }

            // Calculate longest streak
            var previousDate = DateTime.MinValue;
            foreach (var dateGroup in completionsByDate.OrderBy(g => g.Key))
            {
                if (previousDate == DateTime.MinValue || dateGroup.Key == previousDate.AddDays(1))
                {
                    tempStreak++;
                    longestStreak = Math.Max(longestStreak, tempStreak);
                }
                else
                {
                    tempStreak = 1;
                }
                previousDate = dateGroup.Key;
            }

            streakInfo.CurrentStreak = currentStreak;
            streakInfo.LongestStreak = longestStreak;

            // Get recent reminder titles
            streakInfo.RecentReminders = completions
                .Take(10)
                .Select(c => c.ReminderTitle)
                .Distinct()
                .ToList();

            _logger.LogInformation("Retrieved streak info for user {UserId}: Current={CurrentStreak}, Longest={LongestStreak}, Total={TotalCompletions}", 
                userId, currentStreak, longestStreak, completions.Count);

            return streakInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting streak info for user {UserId}", userId);
            // Return default values instead of throwing
            return new UserStreakInfo
            {
                UserId = userId,
                Username = "Error Loading",
                CurrentStreak = 0,
                LongestStreak = 0,
                TotalCompletions = 0,
                LastCompletionDate = null,
                RecentReminders = new List<string>()
            };
        }
    }
    
    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int limit = 50)
    {
        try
        {
            // First check if we have any reminder completions at all
            var totalCompletions = await _reminderCompletionsCollection.CountDocumentsAsync(FilterDefinition<ReminderCompletion>.Empty);
            if (totalCompletions == 0)
            {
                _logger.LogInformation("No reminder completions found in database");
                return new List<LeaderboardEntry>();
            }

            var usersCollectionName = _usersCollection.CollectionNamespace.CollectionName;
            
            var pipeline = new[]
            {
                // Group by user to calculate stats
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$userId" },
                    { "totalCompletions", new BsonDocument("$sum", 1) },
                    { "lastCompletionDate", new BsonDocument("$max", "$completionDate") },
                    { "completionDates", new BsonDocument("$addToSet", "$completionDate") }
                }),
                
                // Convert userId string to ObjectId for lookup
                new BsonDocument("$addFields", new BsonDocument
                {
                    { "userObjectId", new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { new BsonDocument("$type", "$_id"), "objectId" }),
                            "$_id",
                            new BsonDocument("$toObjectId", "$_id")
                        })
                    }
                }),
                
                // Lookup user information
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", usersCollectionName },
                    { "localField", "userObjectId" },
                    { "foreignField", "_id" },
                    { "as", "user" }
                }),
                
                // Filter out users that weren't found
                new BsonDocument("$match", new BsonDocument
                {
                    { "user", new BsonDocument("$ne", new BsonArray()) }
                }),
                
                // Unwind user array
                new BsonDocument("$unwind", "$user"),
                
                // Add computed fields
                new BsonDocument("$addFields", new BsonDocument
                {
                    { "userId", "$_id" },
                    { "username", "$user.username" },
                    { "firstName", new BsonDocument("$ifNull", new BsonArray { "$user.firstName", BsonNull.Value }) },
                    { "lastName", new BsonDocument("$ifNull", new BsonArray { "$user.lastName", BsonNull.Value }) }
                }),
                
                // Sort by total completions desc, then by last completion date desc
                new BsonDocument("$sort", new BsonDocument
                {
                    { "totalCompletions", -1 },
                    { "lastCompletionDate", -1 }
                }),
                
                // Limit results
                new BsonDocument("$limit", limit)
            };

            var aggregationResults = await _reminderCompletionsCollection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync();

            var leaderboard = new List<LeaderboardEntry>();
            var rank = 1;

            foreach (var result in aggregationResults)
            {
                try
                {
                    var userId = result["userId"].AsString;
                    var completionDates = result["completionDates"].AsBsonArray
                        .Select(d => d.ToUniversalTime().Date)
                        .OrderByDescending(d => d)
                        .ToList();

                    // Calculate current and longest streak
                    var (currentStreak, longestStreak) = CalculateStreaks(completionDates);

                    var entry = new LeaderboardEntry
                    {
                        UserId = userId,
                        Username = result["username"].AsString,
                        FirstName = result.Contains("firstName") && !result["firstName"].IsBsonNull ? 
                            result["firstName"].AsString : null,
                        LastName = result.Contains("lastName") && !result["lastName"].IsBsonNull ? 
                            result["lastName"].AsString : null,
                        TotalCompletions = result["totalCompletions"].AsInt32,
                        LastCompletionDate = result.Contains("lastCompletionDate") ? 
                            result["lastCompletionDate"].ToUniversalTime() : null,
                        CurrentStreak = currentStreak,
                        LongestStreak = longestStreak,
                        Rank = rank++
                    };

                    leaderboard.Add(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing leaderboard entry, skipping");
                    continue;
                }
            }

            _logger.LogInformation("Generated leaderboard with {EntryCount} entries from {TotalCompletions} total completions", 
                leaderboard.Count, totalCompletions);
            return leaderboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating leaderboard");
            return new List<LeaderboardEntry>(); // Return empty list instead of throwing
        }
    }
    
    private (int currentStreak, int longestStreak) CalculateStreaks(List<DateTime> completionDates)
    {
        if (completionDates.Count == 0)
            return (0, 0);
        
        var today = DateTime.UtcNow.Date;
        var currentStreak = 0;
        var longestStreak = 0;
        var tempStreak = 0;
        
        // Calculate current streak
        var checkDate = today;
        foreach (var date in completionDates)
        {
            if (date == checkDate || date == checkDate.AddDays(-1))
            {
                currentStreak++;
                checkDate = date.AddDays(-1);
            }
            else
            {
                break;
            }
        }
        
        // Calculate longest streak
        var previousDate = DateTime.MinValue;
        foreach (var date in completionDates.OrderBy(d => d))
        {
            if (previousDate == DateTime.MinValue || date == previousDate.AddDays(1))
            {
                tempStreak++;
                longestStreak = Math.Max(longestStreak, tempStreak);
            }
            else
            {
                tempStreak = 1;
            }
            previousDate = date;
        }
        
        return (currentStreak, longestStreak);
    }

    // Private helper methods
    private async Task CreateIndexesAsync()
    {
        try
        {
            // Compound index for efficient queries
            await _favoriteRecitersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<FavoriteReciter>(
                    Builders<FavoriteReciter>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.IsActive)
                        .Ascending(x => x.Order),
                    new CreateIndexOptions { Name = "userId_isActive_order" }
                )
            );
            
            // Unique constraint for userId + reciterId combination
            await _favoriteRecitersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<FavoriteReciter>(
                    Builders<FavoriteReciter>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.ReciterId),
                    new CreateIndexOptions { Unique = true, Name = "userId_reciterId_unique" }
                )
            );
            
            // Index for analytics
            await _favoriteRecitersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<FavoriteReciter>(
                    Builders<FavoriteReciter>.IndexKeys.Ascending(x => x.ReciterId),
                    new CreateIndexOptions { Name = "reciterId" }
                )
            );
            
            // Reminder Completions indexes
            await _reminderCompletionsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReminderCompletion>(
                    Builders<ReminderCompletion>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Descending(x => x.CompletionDate),
                    new CreateIndexOptions { Name = "userId_completionDate" }
                )
            );
            
            await _reminderCompletionsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReminderCompletion>(
                    Builders<ReminderCompletion>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.ReminderId)
                        .Ascending(x => x.CompletionDate),
                    new CreateIndexOptions { Name = "userId_reminderId_completionDate" }
                )
            );
            
            // UNIQUE compound index for duplicate prevention - CRITICAL for preventing race conditions
            await _reminderCompletionsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReminderCompletion>(
                    Builders<ReminderCompletion>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.ReminderId)
                        .Ascending(x => x.CompletionDate)
                        .Ascending(x => x.DeviceId),
                    new CreateIndexOptions { 
                        Unique = true,
                        Name = "userId_reminderId_completionDate_deviceId_unique"
                    }
                )
            );
            
            _logger.LogInformation("Database indexes created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database indexes");
            // Don't throw - indexes are nice to have but not critical for startup
        }
    }

    private async Task<int> GetNextOrderForUserAsync(string userId)
    {
        var maxOrder = await _favoriteRecitersCollection
            .Find(x => x.UserId == userId)
            .SortByDescending(x => x.Order)
            .Project(x => x.Order)
            .FirstOrDefaultAsync();
            
        return maxOrder + 1;
    }
}