using Hangfire;
using server.Models;

namespace server.Services;

public class ReminderBackgroundService
{
    private readonly SimpleDbService _db;
    private readonly PushNotificationService _pushService;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(SimpleDbService db, PushNotificationService pushService, ILogger<ReminderBackgroundService> logger)
    {
        _db = db;
        _pushService = pushService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessPendingRemindersAsync()
    {
        try
        {
            _logger.LogInformation("🔔 Starting reminder processing job");

            var now = DateTime.UtcNow;
            var currentTime = now.TimeOfDay;
            var currentDay = now.Date;

            // Get all active reminders that should be processed
            var pendingNotifications = new List<(User user, Reminder reminder)>();

            // This is a simplified approach - in production you'd want more efficient queries
            var allUsers = await _db.GetAllUsersAsync();
            
            foreach (var user in allUsers)
            {
                var userReminders = await _db.GetUserRemindersAsync(user.Id);
                
                foreach (var reminder in userReminders)
                {
                    // Skip if completed today
                    if (reminder.IsCompletedToday)
                        continue;

                    // Check if current time is within reminder window
                    if (IsTimeInReminderWindow(currentTime, reminder))
                    {
                        // Check if we should send a notification (not sent recently)
                        var shouldSend = await ShouldSendNotificationAsync(user.Id, reminder.Id, now);
                        
                        if (shouldSend)
                        {
                            pendingNotifications.Add((user, reminder));
                        }
                    }
                }
            }

            _logger.LogInformation($"📱 Found {pendingNotifications.Count} pending notifications");

            if (pendingNotifications.Any())
            {
                // Send notifications in batches
                const int batchSize = 100;
                for (int i = 0; i < pendingNotifications.Count; i += batchSize)
                {
                    var batch = pendingNotifications.Skip(i).Take(batchSize).ToList();
                    await _pushService.SendBulkNotificationsAsync(batch);
                    
                    // Small delay between batches to avoid rate limiting
                    if (i + batchSize < pendingNotifications.Count)
                    {
                        await Task.Delay(1000);
                    }
                }
            }

            _logger.LogInformation("✅ Reminder processing job completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in reminder processing job");
            throw; // Re-throw to let Hangfire handle retry logic
        }
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessDailyStreakCheckAsync()
    {
        try
        {
            _logger.LogInformation("🔥 Starting daily streak check job");

            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var allUsers = await _db.GetAllUsersAsync();

            foreach (var user in allUsers)
            {
                try
                {
                    await ProcessUserStreakAsync(user, yesterday);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing streak for user {user.Id}");
                    // Continue with other users
                }
            }

            _logger.LogInformation("✅ Daily streak check completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in daily streak check job");
            throw;
        }
    }

    private bool IsTimeInReminderWindow(TimeSpan currentTime, Reminder reminder)
    {
        // Parse start and end times
        if (!TimeSpan.TryParse(reminder.StartTime, out var startTime) ||
            !TimeSpan.TryParse(reminder.EndTime, out var endTime))
        {
            return false;
        }

        // Handle cases where end time is before start time (crosses midnight)
        if (endTime < startTime)
        {
            return currentTime >= startTime || currentTime <= endTime;
        }
        else
        {
            return currentTime >= startTime && currentTime <= endTime;
        }
    }

    private async Task<bool> ShouldSendNotificationAsync(string userId, string reminderId, DateTime now)
    {
        // Get recent logs for this reminder
        var recentLogs = await _db.GetReminderLogsAsync(userId, now.AddHours(-1), now);
        
        // Check if notification was sent recently (within last 30 minutes)
        var lastNotification = recentLogs
            .Where(l => l.ReminderId == reminderId && l.Action == "notification_sent")
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefault();

        if (lastNotification != null)
        {
            var timeSinceLastNotification = now - lastNotification.Timestamp;
            return timeSinceLastNotification.TotalMinutes >= 30; // Send every 30 minutes
        }

        return true; // No recent notification found
    }

    private async Task ProcessUserStreakAsync(User user, DateTime yesterdayDate)
    {
        // Check if user completed any reminders yesterday
        var yesterdayLogs = await _db.GetReminderLogsAsync(user.Id, yesterdayDate, yesterdayDate.AddDays(1));
        var completedYesterday = yesterdayLogs.Any(l => l.Action == "completed");

        var currentStreak = await _db.GetUserStreakAsync(user.Id);

        if (completedYesterday)
        {
            // User completed reminders yesterday - maintain or increase streak
            if (currentStreak == null)
            {
                // First time completion
                await _db.UpdateUserStreakAsync(user.Id);
                currentStreak = await _db.GetUserStreakAsync(user.Id); // Get updated streak
            }
            else
            {
                // Check if this continues the streak
                var lastCompletion = currentStreak.LastCompletionDate?.Date;
                if (lastCompletion == null || lastCompletion == yesterdayDate.AddDays(-1))
                {
                    // Streak continues
                    await _db.UpdateUserStreakAsync(user.Id);
                    currentStreak = await _db.GetUserStreakAsync(user.Id); // Get updated streak
                    _logger.LogInformation($"User {user.Username} continued streak to {currentStreak?.CurrentStreak}");
                }
            }
        }
        else
        {
            // User didn't complete reminders yesterday - check if streak should be broken
            if (currentStreak != null && currentStreak.CurrentStreak > 0)
            {
                var lastCompletion = currentStreak.LastCompletionDate?.Date;
                if (lastCompletion != null && lastCompletion < yesterdayDate)
                {
                    // Break the streak - user missed a day
                    currentStreak.CurrentStreak = 0;
                    currentStreak.LastCompletionDate = null;
                    await _db.UpdateUserStreakAsync(user.Id);
                    _logger.LogInformation($"Streak broken for user {user.Username} - missed {yesterdayDate:yyyy-MM-dd}");
                }
            }
        }

        // Send streak milestone notifications
        if (currentStreak != null && completedYesterday && currentStreak.CurrentStreak > 0)
        {
            var streakCount = currentStreak.CurrentStreak;
            if (streakCount % 7 == 0 || streakCount % 30 == 0) // Weekly/Monthly milestones
            {
                await _pushService.SendStreakNotificationAsync(user, streakCount);
                _logger.LogInformation($"Sent milestone notification to {user.Username} for {streakCount} day streak");
            }
        }
    }

    public static void ScheduleRecurringJobs()
    {
        // Schedule reminder checking every 5 minutes
        RecurringJob.AddOrUpdate<ReminderBackgroundService>(
            "process-pending-reminders",
            service => service.ProcessPendingRemindersAsync(),
            "*/5 * * * *" // Every 5 minutes
        );

        // Schedule daily streak check at 1 AM UTC
        RecurringJob.AddOrUpdate<ReminderBackgroundService>(
            "daily-streak-check",
            service => service.ProcessDailyStreakCheckAsync(),
            "0 1 * * *" // Daily at 1 AM UTC
        );
    }
} 