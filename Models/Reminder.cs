using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GurabaFiDunya.Models;

public class Reminder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;
    
    [BsonElement("startTime")]
    public string StartTime { get; set; } = string.Empty;
    
    [BsonElement("endTime")]
    public string EndTime { get; set; } = string.Empty;
    
    [BsonElement("frequency")]
    public string Frequency { get; set; } = "daily";
    
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
} 