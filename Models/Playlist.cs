namespace SyncDeck.Models;

public class Playlist
{
    public string  Id         { get; set; } = string.Empty;
    public string  Name       { get; set; } = "Unknown Playlist";
    public int     TrackCount { get; set; }
    public string? ImageUrl   { get; set; }

    public override string ToString() => Name;
}
