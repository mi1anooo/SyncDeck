using SyncDeck.Models;
using SyncDeck.Services.Music;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace SyncDeck.Services.AppleMusic;

/// <summary>
/// Local Apple Music / iTunes controller.
///
/// macOS  → AppleScript (osascript) against Music.app
/// Windows → COM automation against iTunes.Application
///
/// Playlist playback fix (Windows):
///   PlayFirstTrack() is an IITUserPlaylist method and is NOT available on all
///   playlist types (smart playlists, library, etc.). We now use a multi-step
///   fallback: PlayFirstTrack → iterate tracks and Play the first one → Play()
///   on the iTunes app itself after setting the playlist as the current source.
/// </summary>
public class AppleMusicProvider : IMusicProvider, IDisposable
{
    private const string Separator        = "\u001F";
    private const string NotDetectedMsg   = "Apple Music app/iTunes not detected.";

    private readonly Timer _poll = new(1_000) { AutoReset = true };
    private int    _isPolling;
    private Track? _current;
    private double _position;
    private bool   _playing;
    private bool   _connected;
    private string _lastArtTrackId = "";   // skip artwork fetch when same track
    private object? _itunes;

    public string ProviderName    => "Apple Music";
    public bool   IsAuthenticated => _connected;
    public bool   IsPlaying       => _playing;

    public event EventHandler<Track?>  TrackChanged         = delegate { };
    public event EventHandler<bool>    PlaybackStateChanged = delegate { };
    public event EventHandler<double>  ProgressChanged      = delegate { };

    public AppleMusicProvider()
    {
        _poll.Elapsed += async (_, _) => await SafePollAsync();
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    public async Task LoginAsync()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Apple Music local control is only supported on macOS and Windows.");

        try
        {
            if (OperatingSystem.IsMacOS())
                await RunAppleScriptAsync("tell application \"Music\" to launch");
            else
                EnsureITunes();

            _connected = true;
            _poll.Start();
            await SafePollAsync();
        }
        catch (Exception ex)
        {
            _connected = false;
            SetUnavailable(MapFriendlyError(ex));
            throw new InvalidOperationException(MapFriendlyError(ex), ex);
        }
    }

    public Task LogoutAsync()
    {
        _poll.Stop();
        _connected = false;
        _playing   = false;
        _position  = 0;
        PlaybackStateChanged.Invoke(this, false);
        ProgressChanged.Invoke(this, 0);
        return Task.CompletedTask;
    }

    // ── Playback ─────────────────────────────────────────────────────────────

    public async Task<Track?> GetCurrentTrackAsync()
    {
        if (_connected) await SafePollAsync();
        return _current ?? Track.Empty;
    }

    public async Task PlayAsync()
    {
        await EnsureConnectedAsync();
        if (OperatingSystem.IsMacOS())
            await RunAppleScriptAsync("tell application \"Music\" to play");
        else
            InvokeITunes("Play");
        await SafePollAsync();
    }

    public async Task PauseAsync()
    {
        await EnsureConnectedAsync();
        if (OperatingSystem.IsMacOS())
            await RunAppleScriptAsync("tell application \"Music\" to pause");
        else
            InvokeITunes("Pause");
        await SafePollAsync();
    }

    public async Task NextAsync()
    {
        await EnsureConnectedAsync();
        if (OperatingSystem.IsMacOS())
            await RunAppleScriptAsync("tell application \"Music\" to next track");
        else
            InvokeITunes("NextTrack");
        await Task.Delay(300);
        await SafePollAsync();
    }

    public async Task PreviousAsync()
    {
        await EnsureConnectedAsync();
        if (OperatingSystem.IsMacOS())
            await RunAppleScriptAsync("tell application \"Music\" to previous track");
        else
            InvokeITunes("PreviousTrack");
        await Task.Delay(300);
        await SafePollAsync();
    }

    public async Task SeekAsync(double positionSeconds)
    {
        await EnsureConnectedAsync();
        var safe = Math.Max(0, positionSeconds);
        if (OperatingSystem.IsMacOS())
            await RunAppleScriptAsync($"tell application \"Music\" to set player position to {safe.ToString(CultureInfo.InvariantCulture)}");
        else
            SetITunesProperty("PlayerPosition", safe);
        _position = safe;
        ProgressChanged.Invoke(this, _position);
    }

    public Task<double> GetProgressAsync() => Task.FromResult(_position);

    // ── Playlists ─────────────────────────────────────────────────────────────

    public async Task<List<Playlist>> GetPlaylistsAsync()
    {
        await EnsureConnectedAsync();
        try
        {
            return OperatingSystem.IsMacOS()  ? await GetMacPlaylistsAsync()
                 : OperatingSystem.IsWindows() ? GetWindowsPlaylists()
                 : new List<Playlist>();
        }
        catch (Exception ex) { throw new InvalidOperationException(MapFriendlyError(ex), ex); }
    }

    public async Task SetPlaylistAsync(string playlistId)
    {
        if (string.IsNullOrWhiteSpace(playlistId)) return;
        await EnsureConnectedAsync();
        try
        {
            if (OperatingSystem.IsMacOS())
                await PlayMacPlaylistAsync(playlistId);
            else if (OperatingSystem.IsWindows())
                PlayWindowsPlaylist(playlistId);

            await Task.Delay(400);
            await SafePollAsync();
        }
        catch (Exception ex) { throw new InvalidOperationException(MapFriendlyError(ex), ex); }
    }

    public async Task SetShuffleAsync(bool enabled)
    {
        try
        {
            await EnsureConnectedAsync();
            if (OperatingSystem.IsMacOS())
                await RunAppleScriptAsync($"tell application \"Music\" to set shuffle enabled to {(enabled ? "true" : "false")}");
            else
                SetITunesProperty("ShuffleEnabled", enabled);
        }
        catch { /* non-fatal */ }
    }

    private async Task EnsureConnectedAsync()
    {
        if (!_connected) await LoginAsync();
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    private async Task SafePollAsync()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) == 1) return;
        try
        {
            var state = OperatingSystem.IsMacOS()  ? await ReadMacStateAsync()
                      : OperatingSystem.IsWindows() ? ReadWindowsState()
                      : ApplePlaybackState.Unavailable("Apple Music local control is only supported on macOS and Windows.");
            ApplyState(state);
        }
        catch (Exception ex) { ApplyState(ApplePlaybackState.Unavailable(MapFriendlyError(ex))); }
        finally { Interlocked.Exchange(ref _isPolling, 0); }
    }

    private void ApplyState(ApplePlaybackState state)
    {
        if (!state.IsAvailable) { SetUnavailable(state.ErrorMessage ?? NotDetectedMsg); return; }

        var oldId      = _current?.Id;
        var wasPlaying = _playing;

        _current  = state.Track ?? Track.Empty;
        _position = Math.Max(0, state.PositionSeconds);
        _playing  = state.IsPlaying;

        if (oldId != _current.Id)  TrackChanged.Invoke(this, _current);
        if (wasPlaying != _playing) PlaybackStateChanged.Invoke(this, _playing);
        ProgressChanged.Invoke(this, _position);
    }

    private void SetUnavailable(string message)
    {
        var wasPlaying = _playing;
        var prevId     = _current?.Id;

        _playing  = false;
        _position = 0;
        _current  = new Track
        {
            Id     = "apple-unavailable",
            Title  = message,
            Artist = "Open Music.app on macOS or install iTunes on Windows.",
            Album  = "",
            Duration = TimeSpan.Zero
        };

        if (prevId != _current.Id) TrackChanged.Invoke(this, _current);
        if (wasPlaying)            PlaybackStateChanged.Invoke(this, false);
        ProgressChanged.Invoke(this, 0);
    }

    // ── macOS / AppleScript ───────────────────────────────────────────────────

    /// <summary>
    /// Exports the artwork for the current track to a temp PNG via AppleScript,
    /// reads the bytes, then deletes the file. Returns null on any failure.
    /// </summary>
    private static async Task<byte[]?> FetchMacArtworkAsync(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId)) return null;
        try
        {
            var tempPath  = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "syncdeck_art.jpg");
            var safePath  = tempPath.Replace("\\", "/");
            var script = $"""
            if application "Music" is not running then return "SKIP"
            tell application "Music"
                try
                    set t to current track
                    set artList to artworks of t
                    if (count of artList) > 0 then
                        set art to item 1 of artList
                        set artData to raw data of art
                        set f to open for access POSIX file "{safePath}" with write permission
                        set eof f to 0
                        write artData to f
                        close access f
                        return "OK"
                    end if
                end try
            end tell
            return "SKIP"
            """;

            var result = await RunAppleScriptAsync(script);
            if (!result.StartsWith("OK", StringComparison.OrdinalIgnoreCase)) return null;
            if (!System.IO.File.Exists(tempPath)) return null;

            var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
            try { System.IO.File.Delete(tempPath); } catch { /* non-fatal */ }
            return bytes.Length > 0 ? bytes : null;
        }
        catch { return null; }
    }

    private async Task<ApplePlaybackState> ReadMacStateAsync()
    {
        var script = """
        if application "Music" is not running then
            return "NOT_RUNNING"
        end if
        tell application "Music"
            set d to ASCII character 31
            set stateText to player state as string
            if stateText is "stopped" then
                return "STOPPED" & d & "" & d & "" & d & "" & d & "0" & d & "0" & d & stateText
            end if
            try
                set t to current track
                set trackId to ""
                set trackName to "Unknown Track"
                set artistName to "Unknown Artist"
                set albumName to ""
                set durationSeconds to 0
                set positionSeconds to player position
                try
                    set trackId to persistent ID of t as string
                end try
                try
                    set trackName to name of t as string
                end try
                try
                    set artistName to artist of t as string
                end try
                try
                    set albumName to album of t as string
                end try
                try
                    set durationSeconds to duration of t
                end try
                return trackId & d & trackName & d & artistName & d & albumName & d & (durationSeconds as string) & d & (positionSeconds as string) & d & stateText
            on error
                return "STOPPED" & d & "" & d & "" & d & "" & d & "0" & d & "0" & d & stateText
            end try
        end tell
        """;

        var output = await RunAppleScriptAsync(script);
        if (string.IsNullOrWhiteSpace(output) || output.StartsWith("NOT_RUNNING", StringComparison.OrdinalIgnoreCase))
            return ApplePlaybackState.Unavailable(NotDetectedMsg);

        var parts = output.Split(Separator);
        if (parts.Length < 7 || parts[0].Equals("STOPPED", StringComparison.OrdinalIgnoreCase))
            return ApplePlaybackState.Available(Track.Empty, 0, false);

        var duration     = ParseDouble(parts[4]);
        var position     = ParseDouble(parts[5]);
        var trackId      = string.IsNullOrWhiteSpace(parts[0]) ? $"apple-{parts[1]}-{parts[2]}" : parts[0];
        var artworkBytes = (trackId != _lastArtTrackId)
            ? await FetchMacArtworkAsync(trackId)
            : _current?.AlbumArtData;
        if (artworkBytes is not null) _lastArtTrackId = trackId;
        var track = new Track
        {
            Id           = trackId,
            Title        = string.IsNullOrWhiteSpace(parts[1]) ? "Unknown Track"  : parts[1],
            Artist       = string.IsNullOrWhiteSpace(parts[2]) ? "Unknown Artist" : parts[2],
            Album        = parts[3],
            Duration     = TimeSpan.FromSeconds(Math.Max(0, duration)),
            AlbumArtData = artworkBytes
        };
        return ApplePlaybackState.Available(track, position,
            parts[6].Equals("playing", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> RunAppleScriptAsync(string script)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName             = "osascript",
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute      = false,
                CreateNoWindow       = true
            }
        };
        try { process.Start(); }
        catch (Exception ex) { throw new InvalidOperationException(NotDetectedMsg, ex); }

        await process.StandardInput.WriteAsync(script);
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error  = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? NotDetectedMsg : error.Trim());
        return output.Trim();
    }

    private static async Task<List<Playlist>> GetMacPlaylistsAsync()
    {
        var script = """
        if application "Music" is not running then return "NOT_RUNNING"
        tell application "Music"
            set d to ASCII character 31
            set r to ASCII character 30
            set output to ""
            repeat with p in user playlists
                try
                    set playlistName to name of p as string
                    set playlistId to persistent ID of p as string
                    set trackCount to count of tracks of p
                    if playlistName is not "" then
                        set output to output & playlistId & d & playlistName & d & (trackCount as string) & r
                    end if
                end try
            end repeat
            return output
        end tell
        """;
        var output = await RunAppleScriptAsync(script);
        if (string.IsNullOrWhiteSpace(output) || output.StartsWith("NOT_RUNNING", StringComparison.OrdinalIgnoreCase))
            return new List<Playlist>();
        return ParsePlaylistRows(output, "music");
    }

    private static async Task PlayMacPlaylistAsync(string playlistId)
    {
        var id     = playlistId.StartsWith("music:", StringComparison.OrdinalIgnoreCase) ? playlistId[6..] : playlistId;
        var safeId = EscapeAppleScriptString(id);
        var script = $"""
        tell application "Music"
            set targetPlaylist to missing value
            repeat with p in user playlists
                try
                    if (persistent ID of p as string) is "{safeId}" then
                        set targetPlaylist to p
                        exit repeat
                    end if
                end try
            end repeat
            if targetPlaylist is missing value then
                error "Playlist not found."
            end if
            play targetPlaylist
        end tell
        """;
        await RunAppleScriptAsync(script);
    }

    // ── Windows / iTunes COM ──────────────────────────────────────────────────

    private ApplePlaybackState ReadWindowsState()
    {
        try
        {
            EnsureITunes();
            var app          = _itunes!;
            var currentTrack = GetITunesProperty(app, "CurrentTrack");
            var playerState  = Convert.ToInt32(GetITunesProperty(app, "PlayerState"), CultureInfo.InvariantCulture);
            var position     = Convert.ToDouble(GetITunesProperty(app, "PlayerPosition") ?? 0.0, CultureInfo.InvariantCulture);
            var isPlaying    = playerState == 1;

            if (currentTrack is null)
                return ApplePlaybackState.Available(Track.Empty, position, isPlaying);

            var title    = Convert.ToString(TryGet(currentTrack, "Name"),             CultureInfo.InvariantCulture) ?? "Unknown Track";
            var artist   = Convert.ToString(TryGet(currentTrack, "Artist"),           CultureInfo.InvariantCulture) ?? "Unknown Artist";
            var album    = Convert.ToString(TryGet(currentTrack, "Album"),            CultureInfo.InvariantCulture) ?? "";
            var duration = Convert.ToDouble(TryGet(currentTrack, "Duration") ?? 0.0, CultureInfo.InvariantCulture);
            var id       = Convert.ToString(TryGet(currentTrack, "TrackDatabaseID"), CultureInfo.InvariantCulture);

            var computedId   = string.IsNullOrWhiteSpace(id) ? $"itunes-{title}-{artist}" : id;
            var artworkBytes = (computedId != _lastArtTrackId)
                ? FetchITunesArtworkBytes(currentTrack)
                : _current?.AlbumArtData;
            if (artworkBytes is not null) _lastArtTrackId = computedId;
            var track = new Track
            {
                Id           = computedId,
                Title        = string.IsNullOrWhiteSpace(title)  ? "Unknown Track"  : title,
                Artist       = string.IsNullOrWhiteSpace(artist) ? "Unknown Artist" : artist,
                Album        = album,
                Duration     = TimeSpan.FromSeconds(Math.Max(0, duration)),
                AlbumArtData = artworkBytes
            };
            return ApplePlaybackState.Available(track, position, isPlaying);
        }
        catch (Exception ex) { return ApplePlaybackState.Unavailable(MapFriendlyError(ex)); }
    }

    private List<Playlist> GetWindowsPlaylists()
    {
        EnsureITunes();
        var results = new List<Playlist>();
        var sources = GetITunesProperty(_itunes!, "Sources");
        var sourceCount = Convert.ToInt32(TryGet(sources!, "Count") ?? 0, CultureInfo.InvariantCulture);

        for (var si = 1; si <= sourceCount; si++)
        {
            var source    = CollectionItem(sources!, si);
            var playlists = TryGet(source!, "Playlists");
            if (playlists is null) continue;

            var plCount = Convert.ToInt32(TryGet(playlists, "Count") ?? 0, CultureInfo.InvariantCulture);
            for (var pi = 1; pi <= plCount; pi++)
            {
                try
                {
                    var pl   = CollectionItem(playlists, pi);
                    if (pl is null) continue;
                    var name = Convert.ToString(TryGet(pl, "Name"), CultureInfo.InvariantCulture) ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var tc = ItunesTrackCount(pl);
                    if (tc <= 0) continue;
                    results.Add(new Playlist { Id = $"itunes:{si}:{pi}", Name = name, TrackCount = tc });
                }
                catch { /* skip unreadable playlist types */ }
            }
        }
        return results;
    }

    /// <summary>
    /// Robust iTunes COM playlist playback.
    /// Strategy:
    ///   1. Get playlist object by stored source/playlist index.
    ///   2. Try PlayFirstTrack() — works for IITUserPlaylist.
    ///   3. If that fails, get Tracks collection, item(1), call Play() on that track.
    ///   4. If still fails, call iTunes.Play() to resume whatever was last selected.
    /// </summary>
    private void PlayWindowsPlaylist(string playlistId)
    {
        EnsureITunes();

        var parts = playlistId.Split(':');
        if (parts.Length != 3
            || !int.TryParse(parts[1], out var si)
            || !int.TryParse(parts[2], out var pi))
            throw new InvalidOperationException("Invalid iTunes playlist id.");

        var sources   = GetITunesProperty(_itunes!, "Sources");
        var source    = CollectionItem(sources!, si);
        var playlists = TryGet(source!, "Playlists")
                        ?? throw new InvalidOperationException("No iTunes playlists found.");
        var playlist  = CollectionItem(playlists, pi)
                        ?? throw new InvalidOperationException("Playlist not found.");

        // ── Attempt 1: PlayFirstTrack() ───────────────────────────────────────
        var attempt1Ok = false;
        try
        {
            Invoke(playlist, "PlayFirstTrack", BindingFlags.InvokeMethod);
            attempt1Ok = true;
        }
        catch { /* fall through */ }

        if (attempt1Ok) return;

        // ── Attempt 2: Get first track → Play() on the track object ───────────
        var attempt2Ok = false;
        try
        {
            var tracks = TryGet(playlist, "Tracks");
            if (tracks is not null)
            {
                var firstTrack = CollectionItem(tracks, 1);
                if (firstTrack is not null)
                {
                    Invoke(firstTrack, "Play", BindingFlags.InvokeMethod);
                    attempt2Ok = true;
                }
            }
        }
        catch { /* fall through */ }

        if (attempt2Ok) return;

        // ── Attempt 3: Set playlist as selected source then call iTunes.Play() ──
        try
        {
            // Try to set the current playlist property
            _itunes!.GetType().InvokeMember("CurrentPlaylist",
                BindingFlags.SetProperty, null, _itunes, new[] { playlist });
        }
        catch { /* property may be read-only on some versions */ }

        // Last resort — just hit Play (will play whatever is current)
        try { InvokeITunes("Play"); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start iTunes playback. Make sure iTunes is open and a track is selected.", ex);
        }
    }

    private static int ItunesTrackCount(object playlist)
    {
        try
        {
            var tracks = TryGet(playlist, "Tracks");
            return Convert.ToInt32(TryGet(tracks!, "Count") ?? 0, CultureInfo.InvariantCulture);
        }
        catch { return 0; }
    }

    /// <summary>
    /// Extracts artwork from the iTunes COM track object via SaveArtworkToFile.
    /// Falls back gracefully to null on any failure.
    /// </summary>
    private static byte[]? FetchITunesArtworkBytes(object? track)
    {
        if (track is null) return null;
        try
        {
            var artworks   = TryGet(track, "Artwork");
            if (artworks is null) return null;
            var count = Convert.ToInt32(TryGet(artworks, "Count") ?? 0, CultureInfo.InvariantCulture);
            if (count <= 0) return null;

            var art     = CollectionItem(artworks, 1);
            if (art is null) return null;

            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "syncdeck_art.jpg");
            // SaveArtworkToFile(path) writes the art as JPEG
            Invoke(art, "SaveArtworkToFile", BindingFlags.InvokeMethod, tempPath);

            if (!System.IO.File.Exists(tempPath)) return null;
            var bytes = System.IO.File.ReadAllBytes(tempPath);
            try { System.IO.File.Delete(tempPath); } catch { /* non-fatal */ }
            return bytes.Length > 0 ? bytes : null;
        }
        catch { return null; }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void EnsureITunes()
    {
        if (_itunes is not null) return;
        var type = Type.GetTypeFromProgID("iTunes.Application")
                   ?? throw new InvalidOperationException(NotDetectedMsg);
        _itunes = Activator.CreateInstance(type)
                  ?? throw new InvalidOperationException(NotDetectedMsg);
    }

    private void InvokeITunes(string method, params object?[] args)
    {
        EnsureITunes();
        Invoke(_itunes!, method, BindingFlags.InvokeMethod, args);
    }

    private static object? Invoke(object target, string member, BindingFlags flags, params object?[] args)
        => target.GetType().InvokeMember(member, flags, null, target, args.Length == 0 ? null : args);

    private static object? CollectionItem(object collection, int index)
    {
        try   { return Invoke(collection, "Item", BindingFlags.InvokeMethod, index); }
        catch { return Invoke(collection, "Item", BindingFlags.GetProperty,  index); }
    }

    private static object? GetITunesProperty(object target, string prop)
        => target.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, target, null);

    private static object? TryGet(object target, string prop)
    {
        try { return GetITunesProperty(target, prop); }
        catch { return null; }
    }

    private void SetITunesProperty(string prop, object value)
    {
        EnsureITunes();
        _itunes!.GetType().InvokeMember(prop, BindingFlags.SetProperty, null, _itunes, new[] { value });
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static List<Playlist> ParsePlaylistRows(string output, string prefix)
    {
        var results = new List<Playlist>();
        foreach (var row in output.Split('\u001E', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = row.Split(Separator);
            if (p.Length < 3 || string.IsNullOrWhiteSpace(p[1])) continue;
            results.Add(new Playlist
            {
                Id         = $"{prefix}:{p[0]}",
                Name       = p[1],
                TrackCount = (int)Math.Max(0, ParseDouble(p[2]))
            });
        }
        return results;
    }

    private static string EscapeAppleScriptString(string v)
        => v.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"",  StringComparison.Ordinal);

    private static double ParseDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) return r;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture,   out var r2)) return r2;
        return 0;
    }

    private static string MapFriendlyError(Exception ex)
    {
        var t = ex.ToString();
        if (t.Contains("not authorized",      StringComparison.OrdinalIgnoreCase) ||
            t.Contains("-1743",               StringComparison.OrdinalIgnoreCase))
            return "SyncDeck needs permission to control Music.app. Allow it in System Settings > Privacy & Security > Automation.";
        if (t.Contains("iTunes.Application",  StringComparison.OrdinalIgnoreCase) ||
            t.Contains("osascript",           StringComparison.OrdinalIgnoreCase))
            return NotDetectedMsg;
        return string.IsNullOrWhiteSpace(ex.Message) ? NotDetectedMsg : ex.Message;
    }

    public void Dispose() => _poll.Dispose();

    private sealed record ApplePlaybackState(bool IsAvailable, Track? Track, double PositionSeconds, bool IsPlaying, string? ErrorMessage)
    {
        public static ApplePlaybackState Available(Track? track, double pos, bool playing)
            => new(true, track, pos, playing, null);
        public static ApplePlaybackState Unavailable(string msg)
            => new(false, null, 0, false, msg);
    }
}
