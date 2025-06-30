using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace server.Models;

// SIMPLIFIED USER MODEL
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("email")]
    [Required]
    public string Email { get; set; } = string.Empty;
    
    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;
    
    [BsonElement("username")]
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [BsonElement("firstName")]
    public string FirstName { get; set; } = string.Empty;
    
    [BsonElement("lastName")]
    public string LastName { get; set; } = string.Empty;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // FIX: Add backward compatibility fields for existing database users
    [BsonElement("updatedAt")]
    [BsonIgnoreIfNull]
    public DateTime? UpdatedAt { get; set; }
    
    [BsonElement("roles")]
    [BsonIgnoreIfNull]
    public List<string>? Roles { get; set; }
}

// UPDATED REMINDER MODEL - SUPPORTS TIME RANGE SYSTEM
public class Reminder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("title")]
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [BsonElement("startTime")]
    [Required]
    public string StartTime { get; set; } = string.Empty; // e.g. "08:00"
    
    [BsonElement("endTime")]
    [Required]
    public string EndTime { get; set; } = string.Empty;   // e.g. "20:00"
    
    [BsonElement("isCompletedToday")]
    public bool IsCompletedToday { get; set; } = false;
    
    [BsonElement("lastCompletionDate")]
    public DateTime? LastCompletionDate { get; set; }
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Computed property: Calculate 6 notification times
    [BsonIgnore]
    public List<string> CalculatedTimes
    {
        get
        {
            return CalculateNotificationTimes(StartTime, EndTime);
        }
    }
    
    [BsonIgnore]
    public string FormattedTimeRange => $"{StartTime} - {EndTime} (6 meldingen)";
    
    private static List<string> CalculateNotificationTimes(string start, string end)
    {
        var startMinutes = TimeToMinutes(start);
        var endMinutes = TimeToMinutes(end);
        
        if (endMinutes <= startMinutes) return new List<string>();
        
        var totalDuration = endMinutes - startMinutes;
        var interval = (double)totalDuration / 5.0; // FIX: Use decimal division for even spacing
        
        var times = new List<string>();
        for (int i = 0; i <= 5; i++)
        {
            var notificationMinutes = (int)Math.Round(startMinutes + (interval * i));
            times.Add(MinutesToTime(notificationMinutes));
        }
        
        return times;
    }
    
    private static int TimeToMinutes(string time)
    {
        var parts = time.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int hours) || !int.TryParse(parts[1], out int minutes))
            return 0;
        return hours * 60 + minutes;
    }
    
    private static string MinutesToTime(int minutes)
    {
        var hours = minutes / 60;
        var mins = minutes % 60;
        return $"{hours:D2}:{mins:D2}";
    }
    
    // Daily reset check
    public void CheckDailyReset()
    {
        if (LastCompletionDate.HasValue && LastCompletionDate.Value.Date < DateTime.UtcNow.Date)
        {
            IsCompletedToday = false;
            LastCompletionDate = null;
        }
    }
}

// SIMPLIFIED REMINDER LOG FOR ADMIN TRACKING
public class ReminderLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("reminderId")]
    public string ReminderId { get; set; } = string.Empty;
    
    [BsonElement("reminderTitle")]
    public string ReminderTitle { get; set; } = string.Empty;
    
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty; // "sent", "done", "ignored", "snoozed", "completed"
    
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [BsonElement("deviceId")]
    public string? DeviceId { get; set; }
}

// SIMPLIFIED STREAK TRACKING
public class UserStreak
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("currentStreak")]
    public int CurrentStreak { get; set; } = 0;
    
    [BsonElement("longestStreak")]
    public int LongestStreak { get; set; } = 0;
    
    [BsonElement("lastCompletionDate")]
    public DateTime? LastCompletionDate { get; set; }
    
    [BsonElement("totalCompletions")]
    public int TotalCompletions { get; set; } = 0;
}

// KEEP EXISTING - THIS IS FINE
public class FavoriteReciter
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("reciterId")]
    public string ReciterId { get; set; } = string.Empty;
    
    [BsonElement("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("order")]
    public int Order { get; set; } = 0;
}

// UPDATED DTOs
public class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}

// NEW: Secure DTO for user registration
public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    public string FirstName { get; set; } = string.Empty;
    
    public string LastName { get; set; } = string.Empty;
}

public class CreateReminderRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string StartTime { get; set; } = string.Empty;
    
    [Required]
    public string EndTime { get; set; } = string.Empty;
}

public class LogReminderActionRequest
{
    [Required]
    public string ReminderId { get; set; } = string.Empty;
    
    [Required]
    public string Action { get; set; } = string.Empty; // "done", "ignored", "snoozed"
    
    public string? DeviceId { get; set; }
}

public class MarkReminderCompleteRequest
{
    [Required]
    public string ReminderId { get; set; } = string.Empty;
    
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int TotalCompletions { get; set; }
    public int Rank { get; set; }
} 