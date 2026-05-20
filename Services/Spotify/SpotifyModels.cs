using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SyncDeck.Services.Spotify;

public record SpotifyTokenResponse(
    [property: JsonPropertyName("access_token")]  string  AccessToken,
    [property: JsonPropertyName("token_type")]    string  TokenType,
    [property: JsonPropertyName("expires_in")]    int     ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("scope")]         string? Scope
);

public class SpotifyPlayerState
{
    [JsonPropertyName("is_playing")]  public bool              IsPlaying   { get; set; }
    [JsonPropertyName("progress_ms")] public int               ProgressMs  { get; set; }
    [JsonPropertyName("item")]        public SpotifyTrackItem? Item        { get; set; }
}

public class SpotifyTrackItem
{
    [JsonPropertyName("id")]          public string              Id          { get; set; } = "";
    [JsonPropertyName("name")]        public string              Name        { get; set; } = "";
    [JsonPropertyName("duration_ms")] public int                 DurationMs  { get; set; }
    [JsonPropertyName("artists")]     public List<SpotifyArtist> Artists     { get; set; } = new();
    [JsonPropertyName("album")]       public SpotifyAlbum?       Album       { get; set; }
}

public class SpotifyArtist
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class SpotifyAlbum
{
    [JsonPropertyName("name")]   public string             Name   { get; set; } = "";
    [JsonPropertyName("images")] public List<SpotifyImage> Images { get; set; } = new();
}

public class SpotifyImage
{
    [JsonPropertyName("url")]    public string Url    { get; set; } = "";
    [JsonPropertyName("width")]  public int    Width  { get; set; }
    [JsonPropertyName("height")] public int    Height { get; set; }
}

public class SpotifyPagedPlaylists
{
    [JsonPropertyName("items")] public List<SpotifyPlaylistItem> Items { get; set; } = new();
}

public class SpotifyPlaylistItem
{
    [JsonPropertyName("id")]     public string                  Id     { get; set; } = "";
    [JsonPropertyName("name")]   public string                  Name   { get; set; } = "";
    [JsonPropertyName("tracks")] public SpotifyPlaylistTracks?  Tracks { get; set; }
}

public class SpotifyPlaylistTracks
{
    [JsonPropertyName("total")] public int Total { get; set; }
}
