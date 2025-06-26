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
        private readonly SemaphoreSlim _processingSemaphore = new SemaphoreSlim(1, 1);
        private long _isProcessingFlag = 0;

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

        private void DoWork(object? state)
        {
            // Use atomic operation to prevent race condition
            if (Interlocked.CompareExchange(ref _isProcessingFlag, 1, 0) != 0)
            {
                _logger.LogWarning("Previous reminder processing still in progress, skipping this cycle");
                return;
            }

            // Use Task.Run to handle async work properly
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessDueReminders();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing reminders");
                }
                finally
                {
                    // Reset the flag using atomic operation
                    Interlocked.Exchange(ref _isProcessingFlag, 0);
                }
            });
        }

        private async Task ProcessDueReminders()
        {
            // Use semaphore to ensure only one execution at a time
            if (!await _processingSemaphore.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                _logger.LogWarning("Could not acquire processing semaphore, skipping this cycle");
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                _logger.LogDebug("Starting reminder processing at {Time}", now);
                
                // Get all active reminders that are due
                var dueReminders = await _mongoDbService.GetDueRemindersAsync(now);
                
                _logger.LogInformation("Found {Count} due reminders to process", dueReminders.Count());
                
                foreach (var reminder in dueReminders)
                {
                    try
                    {
                        await ProcessReminder(reminder);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing reminder {ReminderId}", reminder.Id);
                        // Continue processing other reminders even if one fails
                    }
                }
                
                _logger.LogDebug("Completed reminder processing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in ProcessDueReminders");
                throw; // Re-throw to ensure proper error handling
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        private async Task ProcessReminder(Reminder reminder)
        {
            var now = DateTime.UtcNow;
            
            // Check if reminder is snoozed
            if (reminder.SnoozeUntil.HasValue && reminder.SnoozeUntil.Value > now)
            {
                _logger.LogDebug("Reminder {ReminderId} is still snoozed until {SnoozeUntil}", 
                    reminder.Id, reminder.SnoozeUntil.Value);
                return;
            }

            // Check if reminder is within its time range
            if (!IsReminderInTimeRange(reminder, now))
            {
                _logger.LogDebug("Reminder {ReminderId} is not in time range", reminder.Id);
                return;
            }

            // Check if we've already sent a reminder recently (within 5 minutes)
            if (reminder.LastReminderSent.HasValue && 
                (now - reminder.LastReminderSent.Value).TotalMinutes < 5)
            {
                _logger.LogDebug("Reminder {ReminderId} was already sent recently", reminder.Id);
                return;
            }

            try
            {
                // Send notification (in a real app, this would integrate with a notification service)
                await SendReminderNotification(reminder);
                
                // Update reminder stats (this also sets LastReminderSent to DateTime.UtcNow)
                await _mongoDbService.IncrementReminderCountAsync(reminder.Id);
                
                // Clear snooze if it was snoozed
                if (reminder.SnoozeUntil.HasValue)
                {
                    await _mongoDbService.ClearReminderSnoozeAsync(reminder.Id);
                }
                
                _logger.LogInformation("Successfully processed reminder {ReminderId} for user {UserId}", 
                    reminder.Id, reminder.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process reminder {ReminderId}", reminder.Id);
                throw; // Re-throw to be caught by the caller
            }
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

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Reminder Background Service is stopping.");
            
            // Stop the timer first to prevent new processing from starting
            _timer?.Change(Timeout.Infinite, 0);
            
            // Wait for any ongoing processing to complete
            // Use a more robust approach that doesn't rely on flag checking
            _logger.LogInformation("Waiting for ongoing reminder processing to complete...");
            var timeout = TimeSpan.FromSeconds(30);
            
            try
            {
                // Try to acquire the semaphore to ensure no processing is happening
                if (await _processingSemaphore.WaitAsync(timeout, cancellationToken))
                {
                    _logger.LogInformation("Successfully acquired processing semaphore, no ongoing processing");
                    _processingSemaphore.Release();
                }
                else
                {
                    _logger.LogWarning("Timeout waiting for reminder processing to complete");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Stop operation was cancelled while waiting for processing to complete");
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("Processing semaphore was disposed while waiting for processing to complete");
            }
            
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            _processingSemaphore?.Dispose();
            base.Dispose();
        }
    }
} 