namespace server.Models;

public class FavoriteReciterDto
{
    public string ReciterId { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public int Order { get; set; }
    public int ListenCount { get; set; }
    public DateTime? LastListenedAt { get; set; }
}

public class FavoriteReciterStatsDto
{
    public string ReciterId { get; set; } = string.Empty;
    public int FavoriteCount { get; set; }
    public int TotalListens { get; set; }
}

public class AddFavoriteReciterRequest
{
    public string ReciterId { get; set; } = string.Empty;
}

public class ReorderFavoriteRecitersRequest
{
    public List<string> ReciterIds { get; set; } = new();
} 