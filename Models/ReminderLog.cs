using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GurabaFiDunya.Models;

public class ReminderLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("reminderId")]
    public string ReminderId { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("date")]
    public DateOnly Date { get; set; }
    
    [BsonElement("status")]
    public string Status { get; set; } = "pending"; // pending, completed, skipped
    
    [BsonElement("timeMarked")]
    public DateTime? TimeMarked { get; set; }
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
} 