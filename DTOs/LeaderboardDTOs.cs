namespace GurabaFiDunya.DTOs;

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StreakCount { get; set; }
    public DateTime LastActiveDate { get; set; }
    public int Rank { get; set; }
}

public class LeaderboardResponse
{
    public List<LeaderboardEntry> Entries { get; set; } = new();
    public int TotalUsers { get; set; }
    public int UserRank { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class ReciterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string Subdirectory { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
}

public class SurahInfo
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string NameTranslation { get; set; } = string.Empty;
    public int Verses { get; set; }
    public string RevelationType { get; set; } = string.Empty;
}

public class QuranMetadataResponse
{
    public List<ReciterInfo> Reciters { get; set; } = new();
    public List<SurahInfo> Surahs { get; set; } = new();
}

public class AudioUrlResponse
{
    public string AudioUrl { get; set; } = string.Empty;
    public string ReciterName { get; set; } = string.Empty;
    public string SurahName { get; set; } = string.Empty;
    public int SurahNumber { get; set; }
} 