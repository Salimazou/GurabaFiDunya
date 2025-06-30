using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace server.Models;

/// <summary>
/// Represents a log entry for local reminder notifications and user interactions
/// </summary>
public class LocalReminderLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [Required]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("reminderId")]
    [Required]
    public string ReminderId { get; set; } = string.Empty;

    [BsonElement("reminderTitle")]
    [Required]
    public string ReminderTitle { get; set; } = string.Empty;

    [BsonElement("logType")]
    [Required]
    public LocalReminderLogType LogType { get; set; }

    [BsonElement("notificationDate")]
    public DateTime NotificationDate { get; set; }

    [BsonElement("notificationTime")]
    public DateTime NotificationTime { get; set; }

    [BsonElement("scheduledTime")]
    public DateTime? ScheduledTime { get; set; }

    [BsonElement("userResponse")]
    public string? UserResponse { get; set; }

    [BsonElement("responseTime")]
    public DateTime? ResponseTime { get; set; }

    [BsonElement("responseDelayMinutes")]
    public int? ResponseDelayMinutes { get; set; }

    [BsonElement("notificationIndex")]
    public int? NotificationIndex { get; set; }

    [BsonElement("totalNotificationsScheduled")]
    public int? TotalNotificationsScheduled { get; set; }

    [BsonElement("reminderStartTime")]
    public DateTime? ReminderStartTime { get; set; }

    [BsonElement("reminderEndTime")]
    public DateTime? ReminderEndTime { get; set; }

    [BsonElement("deviceId")]
    public string? DeviceId { get; set; }

    [BsonElement("appVersion")]
    public string? AppVersion { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Types of local reminder log entries
/// </summary>
public enum LocalReminderLogType
{
    NotificationScheduled,   // When notifications are scheduled
    NotificationSent,        // When notification is actually sent
    UserResponseDone,        // User marked as done
    UserResponseNotYet,      // User snoozed
    UserResponseTomorrow,    // User postponed to tomorrow
    ReminderCreated,         // When reminder is created
    ReminderDeleted,         // When reminder is deleted
    ReminderModified         // When reminder is modified
}

/// <summary>
/// DTO for creating a local reminder log
/// </summary>
public class CreateLocalReminderLogRequest
{
    [Required]
    public string ReminderId { get; set; } = string.Empty;

    [Required]
    public string ReminderTitle { get; set; } = string.Empty;

    [Required]
    public LocalReminderLogType LogType { get; set; }

    public DateTime? NotificationDate { get; set; }
    public DateTime? NotificationTime { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public string? UserResponse { get; set; }
    public DateTime? ResponseTime { get; set; }
    public int? NotificationIndex { get; set; }
    public int? TotalNotificationsScheduled { get; set; }
    public DateTime? ReminderStartTime { get; set; }
    public DateTime? ReminderEndTime { get; set; }
    public string? DeviceId { get; set; }
    public string? AppVersion { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// DTO for local reminder analytics
/// </summary>
public class LocalReminderAnalytics
{
    public string UserId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalRemindersCreated { get; set; }
    public int TotalNotificationsSent { get; set; }
    public int TotalResponsesDone { get; set; }
    public int TotalResponsesNotYet { get; set; }
    public int TotalResponsesTomorrow { get; set; }
    public double CompletionRate { get; set; }
    public double AverageResponseTimeMinutes { get; set; }
    public List<ReminderEffectivenessDto> ReminderEffectiveness { get; set; } = new();
    public List<DailyActivityDto> DailyActivity { get; set; } = new();
}

/// <summary>
/// DTO for reminder effectiveness analysis
/// </summary>
public class ReminderEffectivenessDto
{
    public string ReminderId { get; set; } = string.Empty;
    public string ReminderTitle { get; set; } = string.Empty;
    public int NotificationsSent { get; set; }
    public int CompletedCount { get; set; }
    public int SnoozedCount { get; set; }
    public int PostponedCount { get; set; }
    public double CompletionRate { get; set; }
    public double AverageResponseTimeMinutes { get; set; }
}

/// <summary>
/// DTO for daily activity
/// </summary>
public class DailyActivityDto
{
    public DateTime Date { get; set; }
    public int NotificationsSent { get; set; }
    public int CompletedTasks { get; set; }
    public int SnoozedTasks { get; set; }
    public int PostponedTasks { get; set; }
    public double CompletionRate { get; set; }
}

/// <summary>
/// DTO for bulk logging notifications
/// </summary>
public class BulkNotificationLogRequest
{
    [Required]
    public string ReminderId { get; set; } = string.Empty;

    [Required]
    public string ReminderTitle { get; set; } = string.Empty;

    [Required]
    public List<NotificationScheduleDto> Notifications { get; set; } = new();

    public DateTime? ReminderStartTime { get; set; }
    public DateTime? ReminderEndTime { get; set; }
    public string? DeviceId { get; set; }
    public string? AppVersion { get; set; }
}

/// <summary>
/// DTO for individual notification schedule
/// </summary>
public class NotificationScheduleDto
{
    public DateTime ScheduledTime { get; set; }
    public DateTime NotificationDate { get; set; }
    public int NotificationIndex { get; set; }
} 