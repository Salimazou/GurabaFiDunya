using MongoDB.Driver;
using server.Models;
using server.Services;

namespace server.Migration;

public class UserMigration
{
    private readonly MongoDbService _mongoDbService;

    public UserMigration(MongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
    }

    public async Task MigrateUsersAsync()
    {
        Console.WriteLine("Starting user migration...");
        
        var users = await _mongoDbService.GetAllUsersAsync();
        int updatedCount = 0;

        foreach (var user in users)
        {
            bool needsUpdate = false;
            var updateBuilder = Builders<User>.Update;
            var updates = new List<UpdateDefinition<User>>();

            // Add missing RefreshToken field
            if (user.RefreshToken == null)
            {
                updates.Add(updateBuilder.Set(u => u.RefreshToken, null));
                needsUpdate = true;
            }

            // Add missing RefreshTokenExpiryTime field
            if (user.RefreshTokenExpiryTime == null)
            {
                updates.Add(updateBuilder.Set(u => u.RefreshTokenExpiryTime, null));
                needsUpdate = true;
            }

            // Add missing UpdatedAt field
            if (user.UpdatedAt == null)
            {
                updates.Add(updateBuilder.Set(u => u.UpdatedAt, null));
                needsUpdate = true;
            }

            // Ensure Roles list exists
            if (user.Roles == null || user.Roles.Count == 0)
            {
                updates.Add(updateBuilder.Set(u => u.Roles, new List<string> { "User" }));
                needsUpdate = true;
            }

            // Ensure FavoriteReciters list exists
            if (user.FavoriteReciters == null)
            {
                updates.Add(updateBuilder.Set(u => u.FavoriteReciters, new List<string>()));
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var combinedUpdate = updateBuilder.Combine(updates);
                await _mongoDbService.UpdateUserAsync(user.Id, combinedUpdate);
                updatedCount++;
                Console.WriteLine($"Updated user: {user.Email}");
            }
        }

        Console.WriteLine($"Migration completed. Updated {updatedCount} users.");
    }
} 