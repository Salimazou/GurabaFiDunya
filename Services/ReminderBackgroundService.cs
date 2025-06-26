using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using server.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace server.Services
{
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly ILogger<ReminderBackgroundService> _logger;
        private readonly MongoDbService _mongoDbService;
        private Timer? _timer;

        public ReminderBackgroundService(ILogger<ReminderBackgroundService> logger, MongoDbService mongoDbService)
        {
            _logger = logger;
            _mongoDbService = mongoDbService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reminder Background Service is starting.");

            // Check for due reminders every minute
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            try
            {
                await ProcessDueReminders();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reminders");
            }
        }

        private async Task ProcessDueReminders()
        {
            var now = DateTime.UtcNow;
            
            // Get all active reminders that are due
            var dueReminders = await _mongoDbService.GetDueRemindersAsync(now);
            
            foreach (var reminder in dueReminders)
            {
                try
                {
                    await ProcessReminder(reminder);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing reminder {ReminderId}", reminder.Id);
                }
            }
        }

        private async Task ProcessReminder(Reminder reminder)
        {
            var now = DateTime.UtcNow;
            
            // Check if reminder is snoozed
            if (reminder.SnoozeUntil.HasValue && reminder.SnoozeUntil.Value > now)
            {
                // Still snoozed, skip for now
                return;
            }

            // Check if reminder is within its time range
            if (!IsReminderInTimeRange(reminder, now))
            {
                return;
            }

            // Send notification (in a real app, this would integrate with a notification service)
            await SendReminderNotification(reminder);
            
            // Update reminder stats
            await _mongoDbService.IncrementReminderCountAsync(reminder.Id);
            
            // Clear snooze if it was snoozed
            if (reminder.SnoozeUntil.HasValue)
            {
                await _mongoDbService.ClearReminderSnoozeAsync(reminder.Id);
            }
            
            _logger.LogInformation("Processed reminder {ReminderId} for user {UserId}", reminder.Id, reminder.UserId);
        }

        private bool IsReminderInTimeRange(Reminder reminder, DateTime now)
        {
            // For specific time reminders
            if (reminder.ReminderType == "specific_time" && !string.IsNullOrEmpty(reminder.ReminderTime))
            {
                if (TimeSpan.TryParse(reminder.ReminderTime, out var reminderTime))
                {
                    var reminderDateTime = now.Date.Add(reminderTime);
                    return Math.Abs((now - reminderDateTime).TotalMinutes) <= 5; // 5-minute window
                }
            }

            // For time range reminders
            if (reminder.ReminderType == "time_range" && 
                !string.IsNullOrEmpty(reminder.ReminderStartTime) && 
                !string.IsNullOrEmpty(reminder.ReminderEndTime))
            {
                if (TimeSpan.TryParse(reminder.ReminderStartTime, out var startTime) &&
                    TimeSpan.TryParse(reminder.ReminderEndTime, out var endTime))
                {
                    var currentTime = now.TimeOfDay;
                    return currentTime >= startTime && currentTime <= endTime;
                }
            }

            // For daily reminders, always allow
            if (reminder.ReminderType == "daily")
            {
                return true;
            }

            return false;
        }

        private async Task SendReminderNotification(Reminder reminder)
        {
            // In a real implementation, this would integrate with:
            // - Push notification service (Firebase, Azure, etc.)
            // - Email service
            // - SMS service
            // - In-app notification system
            
            _logger.LogInformation("Sending notification for reminder: {Title} to user {UserId}", 
                reminder.Title, reminder.UserId);
            
            // For now, just log the notification
            // TODO: Implement actual notification sending
            await Task.CompletedTask; // Placeholder for future implementation
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Reminder Background Service is stopping.");
            
            _timer?.Change(Timeout.Infinite, 0);
            
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
} 