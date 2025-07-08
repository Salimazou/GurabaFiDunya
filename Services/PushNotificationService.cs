using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using server.Models;

namespace server.Services;

public class PushNotificationService
{
    private readonly ILogger<PushNotificationService> _logger;
    private readonly IConfiguration _config;
    private readonly SimpleDbService _db;
    private readonly FirebaseMessaging? _messaging;

    public PushNotificationService(ILogger<PushNotificationService> logger, IConfiguration config, SimpleDbService db)
    {
        _logger = logger;
        _config = config;
        _db = db;

        // Initialize Firebase
        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var credential = GoogleCredential.FromFile(_config["Firebase:ServiceAccountPath"] ?? "firebase-service-account.json");
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = credential,
                    ProjectId = _config["Firebase:ProjectId"]
                });
            }
            _messaging = FirebaseMessaging.DefaultInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Firebase");
            // Continue without Firebase - app should still work with local notifications
        }
    }

    public async Task<bool> SendReminderNotificationAsync(User user, Reminder reminder)
    {
        try
        {
            if (_messaging == null)
            {
                _logger.LogWarning("Firebase messaging not initialized");
                return false;
            }

            if (string.IsNullOrEmpty(user.DeviceToken))
            {
                _logger.LogWarning($"No device token for user {user.Id}");
                return false;
            }

            var message = new Message()
            {
                Token = user.DeviceToken,
                Notification = new Notification()
                {
                    Title = reminder.Title,
                    Body = "Tijd voor je dagelijkse herinnering 🕌"
                },
                Data = new Dictionary<string, string>()
                {
                    {"reminderId", reminder.Id},
                    {"action", "reminder"},
                    {"type", "reminder_notification"}
                },
                Apns = new ApnsConfig()
                {
                    Aps = new Aps()
                    {
                        Sound = "default",
                        Badge = 1,
                        Category = "REMINDER_CATEGORY"
                    }
                }
            };

            var response = await _messaging.SendAsync(message);
            _logger.LogInformation($"Successfully sent message: {response}");

            // Log the notification
            await _db.LogReminderActionAsync(user.Id, reminder.Id, reminder.Title, "notification_sent", user.DeviceToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send push notification to user {user.Id}");
            return false;
        }
    }

    public async Task<bool> SendBulkNotificationsAsync(List<(User user, Reminder reminder)> notifications)
    {
        try
        {
            if (_messaging == null)
            {
                _logger.LogWarning("Firebase messaging not initialized");
                return false;
            }

            var messages = new List<Message>();

            foreach (var (user, reminder) in notifications)
            {
                if (string.IsNullOrEmpty(user.DeviceToken)) continue;

                messages.Add(new Message()
                {
                    Token = user.DeviceToken,
                    Notification = new Notification()
                    {
                        Title = reminder.Title,
                        Body = "Tijd voor je dagelijkse herinnering 🕌"
                    },
                    Data = new Dictionary<string, string>()
                    {
                        {"reminderId", reminder.Id},
                        {"action", "reminder"},
                        {"type", "reminder_notification"}
                    },
                    Apns = new ApnsConfig()
                    {
                        Aps = new Aps()
                        {
                            Sound = "default",
                            Badge = 1,
                            Category = "REMINDER_CATEGORY"
                        }
                    }
                });
            }

            if (!messages.Any())
            {
                _logger.LogWarning("No valid device tokens found for bulk notification");
                return false;
            }

            var response = await _messaging.SendEachAsync(messages);
            _logger.LogInformation($"Bulk send result: {response.SuccessCount} successful, {response.FailureCount} failed");

            // Log successful notifications
            for (int i = 0; i < notifications.Count && i < response.Responses.Count; i++)
            {
                if (response.Responses[i].IsSuccess)
                {
                    var (user, reminder) = notifications[i];
                    await _db.LogReminderActionAsync(user.Id, reminder.Id, reminder.Title, "notification_sent", user.DeviceToken);
                }
            }

            return response.SuccessCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk push notifications");
            return false;
        }
    }

    public async Task<bool> SendStreakNotificationAsync(User user, int streakCount)
    {
        try
        {
            if (_messaging == null)
            {
                _logger.LogWarning("Firebase messaging not initialized");
                return false;
            }

            if (string.IsNullOrEmpty(user.DeviceToken))
            {
                return false;
            }

            var message = new Message()
            {
                Token = user.DeviceToken,
                Notification = new Notification()
                {
                    Title = $"Streak van {streakCount} dagen! 🔥",
                    Body = "Geweldig gedaan! Houd je streak vol!"
                },
                Data = new Dictionary<string, string>()
                {
                    {"action", "streak_notification"},
                    {"type", "achievement"},
                    {"streakCount", streakCount.ToString()}
                },
                Apns = new ApnsConfig()
                {
                    Aps = new Aps()
                    {
                        Sound = "default",
                        Badge = 1
                    }
                }
            };

            var response = await _messaging.SendAsync(message);
            _logger.LogInformation($"Successfully sent streak notification: {response}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send streak notification to user {user.Id}");
            return false;
        }
    }
} 