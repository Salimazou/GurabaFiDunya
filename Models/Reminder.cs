using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace server.Models;

// Custom TimeSpan converter for JSON serialization
public class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            return TimeSpan.Zero;

        // Handle HH:mm:ss format from Swift
        if (TimeSpan.TryParse(value, out var timeSpan))
            return timeSpan;

        // If that fails, try to parse as HH:mm:ss manually
        var parts = value.Split(':');
        if (parts.Length >= 2)
        {
            var hours = int.TryParse(parts[0], out var h) ? h : 0;
            var minutes = int.TryParse(parts[1], out var m) ? m : 0;
            var seconds = parts.Length > 2 && int.TryParse(parts[2], out var s) ? s : 0;
            
            return new TimeSpan(hours, minutes, seconds);
        }

        return TimeSpan.Zero;
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(@"hh\:mm\:ss"));
    }
}

public class Reminder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Time window with custom converter
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan StartTime { get; set; } // e.g., 11:00
    
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan EndTime { get; set; }   // e.g., 23:00
    
    // Frequency and scheduling
    public ReminderFrequency Frequency { get; set; }
    public ReminderType Type { get; set; }
    
    // Status tracking
    public bool IsActive { get; set; } = true;
    public bool IsCompleted { get; set; } = false;
    public DateTime? SnoozedUntil { get; set; }
    public DateTime? PausedUntil { get; set; }
    
    // Interaction tracking
    public string? LastInteractionType { get; set; }
    public DateTime? LastInteractionTime { get; set; }
    public int TimesRemindedToday { get; set; } = 0;
    public int TotalTimesReminded { get; set; } = 0;
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Statistics
    public int CompletedCount { get; set; } = 0;
    public int SkippedCount { get; set; } = 0;
    public int SnoozedCount { get; set; } = 0;
    
    // Failsafe
    public int MaxRemindersPerDay { get; set; } = 6;
    public DateTime LastReminderDate { get; set; } = DateTime.UtcNow.Date;
}

public enum ReminderFrequency
{
    Daily,
    Weekly,
    Custom
}

public enum ReminderType
{
    QuranReading,
    Prayer,
    Study,
    General,
    Exercise,
    Medication
}

public enum ReminderAction
{
    Completed,
    Later,
    Busy,
    Skip
}

public class ReminderInteraction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    public string ReminderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public ReminderAction Action { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    
    // For offline sync
    public string? LocalId { get; set; }
    public bool IsSynced { get; set; } = true;
}

public class ReminderStats
{
    public int TotalReminders { get; set; }
    public int CompletedToday { get; set; }
    public int CompletedThisWeek { get; set; }
    public int CompletedThisMonth { get; set; }
    public double ConsistencyRate { get; set; } // percentage of days with at least one completion
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
}

// Request/Response DTOs
public class CreateReminderRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan StartTime { get; set; }
    
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan EndTime { get; set; }
    
    public ReminderFrequency Frequency { get; set; }
    public ReminderType Type { get; set; }
    public int MaxRemindersPerDay { get; set; } = 3;
}

public class UpdateReminderRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan? StartTime { get; set; }
    
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan? EndTime { get; set; }
    
    public ReminderFrequency? Frequency { get; set; }
    public ReminderType? Type { get; set; }
    public bool? IsActive { get; set; }
    public int? MaxRemindersPerDay { get; set; }
}

public class ReminderInteractionRequest
{
    public ReminderAction Action { get; set; }
    public string? Notes { get; set; }
}

public class SyncInteractionsRequest
{
    public List<OfflineInteraction> Interactions { get; set; } = new();
}

public class OfflineInteraction
{
    public string ReminderId { get; set; } = string.Empty;
    public ReminderAction Action { get; set; }
    public DateTime Timestamp { get; set; }
    public string? LocalId { get; set; }
    public string? Notes { get; set; }
} 