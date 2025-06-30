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
}

// SIMPLIFIED REMINDER MODEL  
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
    
    [BsonElement("notificationTimes")]
    public List<string> NotificationTimes { get; set; } = new(); // ["09:00", "13:00", "18:00"]
    
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
    public string Action { get; set; } = string.Empty; // "sent", "done", "ignored", "snoozed"
    
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

// SIMPLE DTOs
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

public class CreateReminderRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public List<string> NotificationTimes { get; set; } = new();
}

public class LogReminderActionRequest
{
    [Required]
    public string ReminderId { get; set; } = string.Empty;
    
    [Required]
    public string Action { get; set; } = string.Empty; // "done", "ignored", "snoozed"
    
    public string? DeviceId { get; set; }
}

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int TotalCompletions { get; set; }
    public int Rank { get; set; }
} 