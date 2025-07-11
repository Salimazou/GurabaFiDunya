namespace GurabaFiDunya.DTOs;

public class CreateReminderRequest
{
    public string Title { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Frequency { get; set; } = "daily";
}

public class UpdateReminderRequest
{
    public string Title { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Frequency { get; set; } = "daily";
    public bool IsActive { get; set; } = true;
}

public class ReminderResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MarkReminderRequest
{
    public string ReminderId { get; set; } = string.Empty;
    public string Status { get; set; } = "completed"; // completed, skipped
}

public class ReminderLogResponse
{
    public string Id { get; set; } = string.Empty;
    public string ReminderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? TimeMarked { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncRequest
{
    public List<OfflineReminderLog> OfflineLogs { get; set; } = new();
    public DateTime LastSyncTime { get; set; }
}

public class OfflineReminderLog
{
    public string ReminderId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime TimeMarked { get; set; }
}

public class SyncResponse
{
    public List<ReminderLogResponse> ServerLogs { get; set; } = new();
    public List<ReminderResponse> UpdatedReminders { get; set; } = new();
    public DateTime ServerTime { get; set; }
    public bool Success { get; set; }
} 