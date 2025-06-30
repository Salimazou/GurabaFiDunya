using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace server.Models;

public class ReminderCompletion
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("reminderId")]
    public string ReminderId { get; set; } = string.Empty; // UUID from local reminder
    
    [BsonElement("reminderTitle")]
    public string ReminderTitle { get; set; } = string.Empty;
    
    [BsonElement("completedAt")]
    public DateTime CompletedAt { get; set; }
    
    [BsonElement("completionDate")]
    public DateTime CompletionDate { get; set; } // Date only (voor daily tracking)
    
    [BsonElement("syncedAt")]
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("deviceId")]
    public string? DeviceId { get; set; } // Om duplicates te voorkomen bij sync
}

public class ReminderCompletionDto
{
    public string? Id { get; set; }
    public string ReminderId { get; set; } = string.Empty;
    public string ReminderTitle { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public DateTime CompletionDate { get; set; }
    public DateTime SyncedAt { get; set; }
}

public class CreateReminderCompletionRequest
{
    public string ReminderId { get; set; } = string.Empty;
    public string ReminderTitle { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public DateTime CompletionDate { get; set; }
    public string? DeviceId { get; set; }
}

public class SyncReminderCompletionsRequest
{
    public List<CreateReminderCompletionRequest> Completions { get; set; } = new();
}

public class UserStreakInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalCompletions { get; set; }
    public DateTime? LastCompletionDate { get; set; }
    public List<string> RecentReminders { get; set; } = new();
}

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalCompletions { get; set; }
    public DateTime? LastCompletionDate { get; set; }
    public int Rank { get; set; }
} 