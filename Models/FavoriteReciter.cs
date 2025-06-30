using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace server.Models;

public class FavoriteReciter
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("reciterId")]
    public string ReciterId { get; set; } = string.Empty;
    
    [BsonElement("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("order")]
    public int Order { get; set; } = 0;
    
    [BsonElement("listenCount")]
    public int ListenCount { get; set; } = 0;
    
    [BsonElement("lastListenedAt")]
    public DateTime? LastListenedAt { get; set; }
    
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
} 