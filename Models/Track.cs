using System;

namespace SyncDeck.Models;

/// <summary>Represents a single playable music track.</summary>
public class Track
{
    public string   Id           { get; set; } = string.Empty;
    public string   Title        { get; set; } = "Unknown Title";
    public string   Artist       { get; set; } = "Unknown Artist";
    public string   Album        { get; set; } = "Unknown Album";
    public TimeSpan Duration     { get; set; }
    public string?  AlbumArtUrl  { get; set; }
    public byte[]?  AlbumArtData { get; set; }

    public static Track Empty => new()
    {
        Title    = "No Track",
        Artist   = "─ ─ ─",
        Album    = "",
        Duration = TimeSpan.Zero
    };
}
