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
    private readonly IMongoCollection<LocalReminderLog> _localReminderLogsCollection;
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
        _localReminderLogsCollection = _database.GetCollection<LocalReminderLog>(config["MongoDB:LocalReminderLogsCollectionName"]);
        
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
                    
                    // Use UpdateOneModel with upsert=true for atomic operation
                    var update = Builders<ReminderCompletion>.Update
                        .Set(x => x.UserId, reminderCompletion.UserId)
                        .Set(x => x.ReminderId, reminderCompletion.ReminderId)
                        .Set(x => x.ReminderTitle, reminderCompletion.ReminderTitle)
                        .Set(x => x.CompletedAt, reminderCompletion.CompletedAt)
                        .Set(x => x.CompletionDate, reminderCompletion.CompletionDate)
                        .Set(x => x.DeviceId, reminderCompletion.DeviceId)
                        .Set(x => x.SyncedAt, reminderCompletion.SyncedAt);
                    
                    var updateOperation = new UpdateOneModel<ReminderCompletion>(filter, update)
                    {
                        IsUpsert = true
                    };
                    
                    bulkOperations.Add(updateOperation);
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
                catch (MongoBulkWriteException<ReminderCompletion> ex)
                {
                    // Handle duplicate key errors gracefully - this can still happen during high concurrency
                    var duplicateErrors = ex.WriteErrors?.Where(e => e.Code == 11000).ToList() ?? new List<BulkWriteError>();
                    var otherErrors = ex.WriteErrors?.Where(e => e.Code != 11000).ToList() ?? new List<BulkWriteError>();
                    
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

    public async Task<long> CleanNullIdRecordsAsync()
    {
        try
        {
            // Use raw BSON to avoid serialization issues with empty strings
            var filter = new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("_id", BsonNull.Value),
                new BsonDocument("_id", ""),
                new BsonDocument("_id", new BsonDocument("$exists", false))
            });
            
            var result = await _reminderCompletionsCollection.DeleteManyAsync(filter);
            
            _logger.LogInformation("Cleaned up {DeletedCount} reminder completion records with null/empty IDs", result.DeletedCount);
            return result.DeletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning null ID records");
            throw;
        }
    }

    // Local Reminder Log operations
    public async Task<string> CreateLocalReminderLogAsync(string userId, CreateLocalReminderLogRequest request)
    {
        try
        {
            var log = new LocalReminderLog
            {
                UserId = userId,
                ReminderId = request.ReminderId,
                ReminderTitle = request.ReminderTitle,
                LogType = request.LogType,
                NotificationDate = request.NotificationDate ?? DateTime.UtcNow.Date,
                NotificationTime = request.NotificationTime ?? DateTime.UtcNow,
                ScheduledTime = request.ScheduledTime,
                UserResponse = request.UserResponse,
                ResponseTime = request.ResponseTime,
                NotificationIndex = request.NotificationIndex,
                TotalNotificationsScheduled = request.TotalNotificationsScheduled,
                ReminderStartTime = request.ReminderStartTime,
                ReminderEndTime = request.ReminderEndTime,
                DeviceId = request.DeviceId,
                AppVersion = request.AppVersion,
                Metadata = request.Metadata
            };

            // Calculate response delay if both notification and response times are provided
            if (request.ResponseTime.HasValue && request.NotificationTime.HasValue)
            {
                log.ResponseDelayMinutes = (int)(request.ResponseTime.Value - request.NotificationTime.Value).TotalMinutes;
            }

            await _localReminderLogsCollection.InsertOneAsync(log);
            _logger.LogInformation("Created local reminder log for user {UserId}, reminder {ReminderId}, type {LogType}", 
                userId, request.ReminderId, request.LogType);
            
            return log.Id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating local reminder log for user {UserId}", userId);
            throw;
        }
    }

    public async Task BulkCreateNotificationLogsAsync(string userId, BulkNotificationLogRequest request)
    {
        try
        {
            var logs = request.Notifications.Select(notification => new LocalReminderLog
            {
                UserId = userId,
                ReminderId = request.ReminderId,
                ReminderTitle = request.ReminderTitle,
                LogType = LocalReminderLogType.NotificationScheduled,
                NotificationDate = notification.NotificationDate,
                NotificationTime = notification.ScheduledTime,
                ScheduledTime = notification.ScheduledTime,
                NotificationIndex = notification.NotificationIndex,
                TotalNotificationsScheduled = request.Notifications.Count,
                ReminderStartTime = request.ReminderStartTime,
                ReminderEndTime = request.ReminderEndTime,
                DeviceId = request.DeviceId,
                AppVersion = request.AppVersion
            }).ToList();

            if (logs.Any())
            {
                await _localReminderLogsCollection.InsertManyAsync(logs);
                _logger.LogInformation("Bulk created {LogCount} notification logs for user {UserId}, reminder {ReminderId}", 
                    logs.Count, userId, request.ReminderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk creating notification logs for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<LocalReminderLog>> GetLocalReminderLogsAsync(
        string userId, 
        DateTime? startDate = null, 
        DateTime? endDate = null,
        string? reminderId = null,
        LocalReminderLogType? logType = null)
    {
        try
        {
            var filterBuilder = Builders<LocalReminderLog>.Filter;
            var filter = filterBuilder.Eq(x => x.UserId, userId);

            if (startDate.HasValue)
            {
                filter = filterBuilder.And(filter, filterBuilder.Gte(x => x.NotificationDate, startDate.Value));
            }

            if (endDate.HasValue)
            {
                filter = filterBuilder.And(filter, filterBuilder.Lte(x => x.NotificationDate, endDate.Value));
            }

            if (!string.IsNullOrEmpty(reminderId))
            {
                filter = filterBuilder.And(filter, filterBuilder.Eq(x => x.ReminderId, reminderId));
            }

            if (logType.HasValue)
            {
                filter = filterBuilder.And(filter, filterBuilder.Eq(x => x.LogType, logType.Value));
            }

            return await _localReminderLogsCollection
                .Find(filter)
                .SortByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local reminder logs for user {UserId}", userId);
            throw;
        }
    }

    public async Task<LocalReminderAnalytics> GetLocalReminderAnalyticsAsync(string userId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var filter = Builders<LocalReminderLog>.Filter.And(
                Builders<LocalReminderLog>.Filter.Eq(x => x.UserId, userId),
                Builders<LocalReminderLog>.Filter.Gte(x => x.NotificationDate, startDate),
                Builders<LocalReminderLog>.Filter.Lte(x => x.NotificationDate, endDate)
            );

            var logs = await _localReminderLogsCollection.Find(filter).ToListAsync();

            var analytics = new LocalReminderAnalytics
            {
                UserId = userId,
                StartDate = startDate,
                EndDate = endDate
            };

            // Basic counts
            analytics.TotalRemindersCreated = logs.Count(x => x.LogType == LocalReminderLogType.ReminderCreated);
            analytics.TotalNotificationsSent = logs.Count(x => x.LogType == LocalReminderLogType.NotificationSent);
            analytics.TotalResponsesDone = logs.Count(x => x.LogType == LocalReminderLogType.UserResponseDone);
            analytics.TotalResponsesNotYet = logs.Count(x => x.LogType == LocalReminderLogType.UserResponseNotYet);
            analytics.TotalResponsesTomorrow = logs.Count(x => x.LogType == LocalReminderLogType.UserResponseTomorrow);

            // Completion rate
            var totalResponses = analytics.TotalResponsesDone + analytics.TotalResponsesNotYet + analytics.TotalResponsesTomorrow;
            analytics.CompletionRate = totalResponses > 0 ? (double)analytics.TotalResponsesDone / totalResponses * 100 : 0;

                        // Average response time
            var responseTimeLogs = logs.Where(x => x.ResponseDelayMinutes.HasValue).ToList();
            analytics.AverageResponseTimeMinutes = responseTimeLogs.Any() 
                ? responseTimeLogs.Average(x => x.ResponseDelayMinutes!.Value) 
                : 0;

            // Reminder effectiveness
            analytics.ReminderEffectiveness = logs
                .GroupBy(x => new { x.ReminderId, x.ReminderTitle })
                .Select(g => new ReminderEffectivenessDto
                {
                    ReminderId = g.Key.ReminderId,
                    ReminderTitle = g.Key.ReminderTitle,
                    NotificationsSent = g.Count(x => x.LogType == LocalReminderLogType.NotificationSent),
                    CompletedCount = g.Count(x => x.LogType == LocalReminderLogType.UserResponseDone),
                    SnoozedCount = g.Count(x => x.LogType == LocalReminderLogType.UserResponseNotYet),
                    PostponedCount = g.Count(x => x.LogType == LocalReminderLogType.UserResponseTomorrow),
                    CompletionRate = CalculateCompletionRate(g.ToList()),
                    AverageResponseTimeMinutes = CalculateAverageResponseTime(g.ToList())
                })
                .OrderByDescending(x => x.CompletionRate)
                .ToList();

            // Daily activity
            analytics.DailyActivity = logs
                .GroupBy(x => x.NotificationDate.Date)
                .Select(g => new DailyActivityDto
                {
                    Date = g.Key,
                    NotificationsSent = g.Count(x => x.LogType == LocalReminderLogType.NotificationSent),
                    CompletedTasks = g.Count(x => x.LogType == LocalReminderLogType.UserResponseDone),
                    SnoozedTasks = g.Count(x => x.LogType == LocalReminderLogType.UserResponseNotYet),
                    PostponedTasks = g.Count(x => x.LogType == LocalReminderLogType.UserResponseTomorrow),
                    CompletionRate = CalculateCompletionRate(g.ToList())
                })
                .OrderBy(x => x.Date)
                .ToList();

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local reminder analytics for user {UserId}", userId);
            throw;
        }
    }

    private double CalculateCompletionRate(List<LocalReminderLog> logs)
    {
        var completed = logs.Count(x => x.LogType == LocalReminderLogType.UserResponseDone);
        var total = logs.Count(x => x.LogType == LocalReminderLogType.UserResponseDone ||
                                   x.LogType == LocalReminderLogType.UserResponseNotYet ||
                                   x.LogType == LocalReminderLogType.UserResponseTomorrow);
        
        return total > 0 ? (double)completed / total * 100 : 0;
    }

    private double CalculateAverageResponseTime(List<LocalReminderLog> logs)
    {
        var responseTimeLogs = logs.Where(x => x.ResponseDelayMinutes.HasValue).ToList();
        return responseTimeLogs.Any() ? responseTimeLogs.Average(x => x.ResponseDelayMinutes!.Value) : 0;
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

            // Local Reminder Logs indexes
            await _localReminderLogsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<LocalReminderLog>(
                    Builders<LocalReminderLog>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "userId_createdAt" }
                )
            );

            await _localReminderLogsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<LocalReminderLog>(
                    Builders<LocalReminderLog>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.ReminderId)
                        .Descending(x => x.NotificationDate),
                    new CreateIndexOptions { Name = "userId_reminderId_notificationDate" }
                )
            );

            await _localReminderLogsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<LocalReminderLog>(
                    Builders<LocalReminderLog>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.LogType)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "userId_logType_createdAt" }
                )
            );

            await _localReminderLogsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<LocalReminderLog>(
                    Builders<LocalReminderLog>.IndexKeys
                        .Ascending(x => x.NotificationDate)
                        .Ascending(x => x.UserId),
                    new CreateIndexOptions { Name = "notificationDate_userId" }
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