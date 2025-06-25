using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace server.Models;

public class Reminder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("title")]
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = string.Empty;
    
    [BsonElement("description")]
    public string? Description { get; set; }
    
    [BsonElement("isCompleted")]
    public bool IsCompleted { get; set; } = false;
    
    [BsonElement("userId")]
    [Required(ErrorMessage = "User ID is required")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("username")]
    public string? Username { get; set; }
    
    [BsonElement("category")]
    public string? Category { get; set; } = "General";
    
    [BsonElement("priority")]
    public int Priority { get; set; } = 0; // 0 = Low, 1 = Medium, 2 = High
    
    [BsonElement("dueDate")]
    public DateTime? DueDate { get; set; }
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
    
    // Reminder-specific properties
    [BsonElement("reminderType")]
    public string? ReminderType { get; set; } // "specific_time", "daily", "time_range"
    
    [BsonElement("reminderTime")]
    public string? ReminderTime { get; set; } // "16:00" for specific time
    
    [BsonElement("reminderStartTime")]
    public string? ReminderStartTime { get; set; } // "12:00" for time range start
    
    [BsonElement("reminderEndTime")]
    public string? ReminderEndTime { get; set; } // "23:59" for time range end
    
    [BsonElement("isRecurring")]
    public bool? IsRecurring { get; set; } = false;
    
    [BsonElement("lastReminderSent")]
    public DateTime? LastReminderSent { get; set; }
    
    [BsonElement("reminderCount")]
    public int? ReminderCount { get; set; } = 0;
    
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
    
    [BsonElement("tags")]
    public List<string>? Tags { get; set; } = new List<string>();
}

// MARK: - DTOs for API requests/responses
public class CreateReminderDto
{
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    public string? Category { get; set; } = "General";
    public int Priority { get; set; } = 0;
    public DateTime? DueDate { get; set; }
    
    // Reminder-specific fields
    public string? ReminderType { get; set; }
    public string? ReminderTime { get; set; }
    public string? ReminderStartTime { get; set; }
    public string? ReminderEndTime { get; set; }
    public bool? IsRecurring { get; set; } = false;
    public List<string>? Tags { get; set; } = new List<string>();
}

public class UpdateReminderDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool? IsCompleted { get; set; }
    public string? Category { get; set; }
    public int? Priority { get; set; }
    public DateTime? DueDate { get; set; }
    
    // Reminder-specific fields
    public string? ReminderType { get; set; }
    public string? ReminderTime { get; set; }
    public string? ReminderStartTime { get; set; }
    public string? ReminderEndTime { get; set; }
    public bool? IsRecurring { get; set; }
    public bool? IsActive { get; set; }
    public List<string>? Tags { get; set; }
}

public class ReminderStatsDto
{
    public int TotalReminders { get; set; }
    public int CompletedReminders { get; set; }
    public int ActiveReminders { get; set; }
    public int HighPriorityReminders { get; set; }
    public int TodayReminders { get; set; }
    public double CompletionRate { get; set; }
}

public enum ReminderTypeEnum
{
    SpecificTime,
    Daily,
    TimeRange
}

public enum PriorityLevel
{
    Low = 0,
    Medium = 1,
    High = 2
} 