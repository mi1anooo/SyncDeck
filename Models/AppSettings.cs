namespace SyncDeck.Models;

public class AppSettings
{
    public string CurrentTheme    { get; set; } = "SonyChrome";
    public string CurrentProvider { get; set; } = "Mock";
    public bool   AlwaysOnTop     { get; set; } = true;
    public bool   Shuffle         { get; set; } = false;
    public bool   Transparent     { get; set; } = true;
    public double WindowLeft      { get; set; } = 100;
    public double WindowTop       { get; set; } = 100;
    public string? LastPlaylistId { get; set; }
}
