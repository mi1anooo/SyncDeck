using SyncDeck.Models;
using SyncDeck.Services.Music;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace SyncDeck.Services.Spotify;

/// <summary>
/// Controls Spotify playback via the Spotify Web API.
///
/// IMPORTANT API LIMITATIONS:
///   • This is a REMOTE CONTROLLER — it does not stream audio directly.
///   • The user must have Spotify open (and active) on at least one device.
///   • Spotify Premium is required for playback-control Web API calls.
///   • The app polls playback state every ~5 s to detect track changes.
///   • Elapsed progress is interpolated locally between polls for smooth UI.
///
/// SETUP: Set env var SPOTIFY_CLIENT_ID  (see README / appsettings.json).
/// </summary>
public class SpotifyMusicProvider : IMusicProvider, IDisposable
{
    private const string Api = "https://api.spotify.com/v1";

    // TODO: Replace with config loader — do NOT hard-code keys in source.
    private readonly string _clientId =
        Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? "YOUR_SPOTIFY_CLIENT_ID_HERE";

    private readonly HttpClient           _http   = new();
    private readonly SpotifyAuthService   _auth;
    private readonly System.Timers.Timer  _poll   = new(5_000) { AutoReset = true };
    private readonly System.Timers.Timer  _tick   = new(1_000) { AutoReset = true };

    private string?   _accessToken;
    private string?   _refreshToken;
    private DateTime  _tokenExpiry = DateTime.MinValue;

    private Track?   _current;
    private double   _position;
    private bool     _playing;

    // ── IMusicProvider ────────────────────────────────────────────────────────

    public string ProviderName    => "Spotify";
    public bool   IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
    public bool   IsPlaying       => _playing;

    public event EventHandler<Track?> TrackChanged         = delegate { };
    public event EventHandler<bool>   PlaybackStateChanged = delegate { };
    public event EventHandler<double> ProgressChanged      = delegate { };

    public SpotifyMusicProvider()
    {
        _auth = new SpotifyAuthService(_clientId);

        _poll.Elapsed += async (_, _) => await SafePollAsync();
        _tick.Elapsed += (_, _) =>
        {
            if (!_playing) return;
            _position += 1.0;
            ProgressChanged.Invoke(this, _position);
        };
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    public async Task LoginAsync()
    {
        var tok = await _auth.AuthorizeAsync();
        if (tok is null) throw new Exception("Spotify authorization was cancelled or failed.");

        _accessToken  = tok.AccessToken;
        _refreshToken = tok.RefreshToken;
        _tokenExpiry  = DateTime.UtcNow.AddSeconds(tok.ExpiresIn - 60);

        _poll.Start();
        _tick.Start();
        await SafePollAsync(); // immediate state fetch
    }

    public Task LogoutAsync()
    {
        _accessToken  = null;
        _refreshToken = null;
        _tokenExpiry  = DateTime.MinValue;
        _poll.Stop();
        _tick.Stop();
        _playing  = false;
        _position = 0;
        return Task.CompletedTask;
    }

    // ── Playback control ──────────────────────────────────────────────────────

    public async Task PlayAsync()
    {
        var res = await PutAsync($"{Api}/me/player/play", null);
        if (!res.IsSuccessStatusCode)
            throw new Exception(
                "Spotify: Could not start playback. " +
                "Make sure Spotify is open and active on a device, and that you have Premium.");
        _playing = true;
        PlaybackStateChanged.Invoke(this, true);
    }

    public async Task PauseAsync()
    {
        var res = await PutAsync($"{Api}/me/player/pause", null);
        if (res.IsSuccessStatusCode) { _playing = false; PlaybackStateChanged.Invoke(this, false); }
    }

    public async Task NextAsync()
    {
        await PostAsync($"{Api}/me/player/next", null);
        await Task.Delay(600); // give Spotify time to advance
        await SafePollAsync();
    }

    public async Task PreviousAsync()
    {
        await PostAsync($"{Api}/me/player/previous", null);
        await Task.Delay(600);
        await SafePollAsync();
    }

    public async Task SeekAsync(double positionSeconds)
    {
        var ms = (int)(positionSeconds * 1000);
        await PutAsync($"{Api}/me/player/seek?position_ms={ms}", null);
        _position = positionSeconds;
    }

    public Task<double>         GetProgressAsync()  => Task.FromResult(_position);
    public Task<Track?>         GetCurrentTrackAsync() => Task.FromResult(_current);

    public async Task<List<Playlist>> GetPlaylistsAsync()
    {
        var json = await GetAsync($"{Api}/me/playlists?limit=50");
        if (json is null) return new();
        var page = JsonSerializer.Deserialize<SpotifyPagedPlaylists>(json);
        return page?.Items.Select(p => new Playlist
        {
            Id = p.Id, Name = p.Name, TrackCount = p.Tracks?.Total ?? 0
        }).ToList() ?? new();
    }

    public async Task SetPlaylistAsync(string playlistId)
    {
        var body = JsonSerializer.Serialize(new { context_uri = $"spotify:playlist:{playlistId}" });
        await PutAsync($"{Api}/me/player/play", body);
    }

    public async Task SetShuffleAsync(bool enabled)
    {
        await PutAsync($"{Api}/me/player/shuffle?state={enabled.ToString().ToLower()}", null);
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    private async Task SafePollAsync()
    {
        try
        {
            await EnsureTokenAsync();
            var json = await GetAsync($"{Api}/me/player");
            if (json is null) return;

            var state = JsonSerializer.Deserialize<SpotifyPlayerState>(json);
            if (state is null) return;

            var wasPlaying = _playing;
            _playing  = state.IsPlaying;
            _position = state.ProgressMs / 1000.0;

            if (state.Item is not null)
            {
                var track = Map(state.Item);
                if (_current?.Id != track.Id)
                {
                    _current = track;
                    TrackChanged.Invoke(this, _current);
                }
            }

            if (wasPlaying != _playing)
                PlaybackStateChanged.Invoke(this, _playing);

            ProgressChanged.Invoke(this, _position);
        }
        catch { /* Swallow transient poll errors — UI stays up */ }
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    private async Task<string?> GetAsync(string url)
    {
        await EnsureTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        var res = await _http.SendAsync(req);
        if (res.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : null;
    }

    private async Task<HttpResponseMessage> PutAsync(string url, string? json)
    {
        await EnsureTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (json is not null)
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.SendAsync(req);
    }

    private async Task<HttpResponseMessage> PostAsync(string url, string? json)
    {
        await EnsureTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (json is not null)
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.SendAsync(req);
    }

    private async Task EnsureTokenAsync()
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("Not authenticated with Spotify.");
        if (DateTime.UtcNow < _tokenExpiry) return;
        if (_refreshToken is null) throw new InvalidOperationException("No refresh token available.");

        var tok = await _auth.RefreshAsync(_refreshToken);
        if (tok is null) throw new Exception("Failed to refresh Spotify token.");
        _accessToken  = tok.AccessToken;
        if (tok.RefreshToken is not null) _refreshToken = tok.RefreshToken;
        _tokenExpiry  = DateTime.UtcNow.AddSeconds(tok.ExpiresIn - 60);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static Track Map(SpotifyTrackItem i) => new()
    {
        Id          = i.Id,
        Title       = i.Name,
        Artist      = string.Join(", ", i.Artists.Select(a => a.Name)),
        Album       = i.Album?.Name ?? "",
        Duration    = TimeSpan.FromMilliseconds(i.DurationMs),
        AlbumArtUrl = i.Album?.Images.OrderByDescending(x => x.Width).FirstOrDefault()?.Url,
    };

    public void Dispose()
    {
        _poll.Dispose();
        _tick.Dispose();
        _http.Dispose();
    }
}
