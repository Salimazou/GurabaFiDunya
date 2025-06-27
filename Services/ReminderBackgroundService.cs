using server.Models;
using server.Services;

namespace server.Services;

public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReminderBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10);

    public ReminderBackgroundService(IServiceProvider serviceProvider, ILogger<ReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueReminders();
                await ResetCompletedRemindersForNewDay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reminders");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("ReminderBackgroundService stopped");
    }

    private async Task ProcessDueReminders()
    {
        using var scope = _serviceProvider.CreateScope();
        var mongoDbService = scope.ServiceProvider.GetRequiredService<MongoDbService>();
        var pushNotificationService = scope.ServiceProvider.GetService<PushNotificationService>();

        try
        {
            var dueReminders = await mongoDbService.GetDueRemindersAsync();
            
            _logger.LogInformation("Found {Count} due reminders to process", dueReminders.Count);

            foreach (var reminder in dueReminders)
            {
                try
                {
                    // Send notification
                    if (pushNotificationService != null)
                    {
                        await pushNotificationService.SendReminderNotificationAsync(reminder);
                    }
                    else
                    {
                        // Fallback: Log notification (for development)
                        _logger.LogInformation("Sending reminder notification: {Title} to user {UserId}", 
                            reminder.Title, reminder.UserId);
                    }

                    // Increment reminder count
                    await mongoDbService.IncrementReminderCountAsync(reminder.Id);

                    _logger.LogDebug("Processed reminder {ReminderId} for user {UserId}", 
                        reminder.Id, reminder.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing reminder {ReminderId}", reminder.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting due reminders");
        }
    }

    private async Task ResetCompletedRemindersForNewDay()
    {
        using var scope = _serviceProvider.CreateScope();
        var mongoDbService = scope.ServiceProvider.GetRequiredService<MongoDbService>();

        try
        {
            var now = DateTime.UtcNow;
            
            // Only reset once per day around midnight UTC
            if (now.Hour == 0 && now.Minute < 10)
            {
                await mongoDbService.ResetDailyReminderCompletionsAsync();
                _logger.LogInformation("Reset daily reminder completions for new day");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting daily completions");
        }
    }
}

// Optional Push Notification Service Interface and Implementation
public interface IPushNotificationService
{
    Task SendReminderNotificationAsync(Reminder reminder);
}

public class PushNotificationService : IPushNotificationService
{
    private readonly ILogger<PushNotificationService> _logger;
    private readonly IConfiguration _configuration;

    public PushNotificationService(ILogger<PushNotificationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendReminderNotificationAsync(Reminder reminder)
    {
        try
        {
            // This is where you'd integrate with:
            // - Apple Push Notification Service (APNs) for iOS
            // - Firebase Cloud Messaging (FCM) for Android
            // - SignalR for real-time web notifications
            
            var notificationData = new
            {
                reminderId = reminder.Id,
                title = reminder.Title,
                body = reminder.Description,
                data = new
                {
                    type = "reminder",
                    reminderId = reminder.Id,
                    userId = reminder.UserId,
                    reminderType = reminder.Type.ToString()
                }
            };

            _logger.LogInformation("Sending push notification for reminder {ReminderId}: {Title}", 
                reminder.Id, reminder.Title);

            // Placeholder for actual push notification implementation
            // Example implementations:
            
            // For iOS (APNs):
            // await SendApnsNotificationAsync(reminder.UserId, notificationData);
            
            // For Android (FCM):
            // await SendFcmNotificationAsync(reminder.UserId, notificationData);
            
            // For Web (SignalR):
            // await SendSignalRNotificationAsync(reminder.UserId, notificationData);

            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification for reminder {ReminderId}", reminder.Id);
        }
    }

    // Example method signatures for different platforms:
    
    private async Task SendApnsNotificationAsync(string userId, object notificationData)
    {
        // Implementation for Apple Push Notifications
        // You would use a library like dotAPNS or similar
        await Task.CompletedTask;
    }

    private async Task SendFcmNotificationAsync(string userId, object notificationData)
    {
        // Implementation for Firebase Cloud Messaging
        // You would use FirebaseAdmin SDK
        await Task.CompletedTask;
    }

    private async Task SendSignalRNotificationAsync(string userId, object notificationData)
    {
        // Implementation for real-time web notifications
        // You would use SignalR hubs
        await Task.CompletedTask;
    }
} 