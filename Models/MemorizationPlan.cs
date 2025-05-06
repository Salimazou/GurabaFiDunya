using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace server.Models;

public class MemorizationPlan
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;
    
    public string UserId { get; set; } = null!;
    
    public int SurahNumber { get; set; }
    
    public TimeCommitment TimeCommitment { get; set; } = null!;
    
    public bool IncludeRevision { get; set; }
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime StartDate { get; set; }
    
    public int CurrentPageIndex { get; set; }
    
    public double PagesPerDay { get; set; }
    
    public int TotalPages { get; set; }
    
    public List<PageBreakdown> PageBreakdown { get; set; } = new();
    
    public Progress Progress { get; set; } = new();
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CompletionDate { get; set; }
    
    public SurahDetails? SurahDetails { get; set; }
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TimeCommitment
{
    public int Value { get; set; }
    public string Label { get; set; } = null!;
    public double Pages { get; set; }
}

public class PageBreakdown
{
    public int PageNumber { get; set; }
    public int StartSurah { get; set; }
    public int StartAyah { get; set; }
    public int EndSurah { get; set; }
    public int EndAyah { get; set; }
    public bool Completed { get; set; }
    public bool Revised { get; set; }
    public bool Unlocked { get; set; }
}

public class Progress
{
    public List<ProgressItem> Memorized { get; set; } = new();
    public List<ProgressItem> Revised { get; set; } = new();
}

public class ProgressItem
{
    public int PageNumber { get; set; }
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime DateCompleted { get; set; }
}

public class SurahDetails
{
    public int Number { get; set; }
    public string Name { get; set; } = null!;
    public string EnglishName { get; set; } = null!;
    public string EnglishNameTranslation { get; set; } = null!;
    public int NumberOfAyahs { get; set; }
    public string RevelationType { get; set; } = null!;
} 