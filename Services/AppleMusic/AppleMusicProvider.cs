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
/// Local Apple Music controller.
///
/// macOS: controls Music.app through AppleScript / osascript.
/// Windows: controls legacy iTunes through COM automation.
///
/// This intentionally does not use MusicKit yet. MusicKit can be added later
/// for catalogue/library features, but it is not a Spotify-style desktop
/// playback-control API.
/// </summary>
public class AppleMusicProvider : IMusicProvider, IDisposable
{
    private const string Separator = "\u001F";
    private const string NotDetectedMessage = "Apple Music app/iTunes not detected.";

    private readonly Timer _poll = new(1_000) { AutoReset = true };
    private int _isPolling;

    private Track? _current;
    private double _position;
    private bool _playing;
    private bool _connected;
    private object? _itunes;

    public string ProviderName => "Apple Music";
    public bool IsAuthenticated => _connected;
    public bool IsPlaying => _playing;

    public event EventHandler<Track?> TrackChanged = delegate { };
    public event EventHandler<bool> PlaybackStateChanged = delegate { };
    public event EventHandler<double> ProgressChanged = delegate { };

    public AppleMusicProvider()
    {
        _poll.Elapsed += async (_, _) => await SafePollAsync();
    }

    public async Task LoginAsync()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Apple Music local control is only supported on macOS and Windows.");

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                await RunAppleScriptAsync("tell application \"Music\" to launch");
            }
            else
            {
                EnsureITunes();
            }

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
        _playing = false;
        _position = 0;
        PlaybackStateChanged.Invoke(this, false);
        ProgressChanged.Invoke(this, 0);
        return Task.CompletedTask;
    }

    public async Task<Track?> GetCurrentTrackAsync()
    {
        if (_connected)
            await SafePollAsync();

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
        var safePosition = Math.Max(0, positionSeconds);

        if (OperatingSystem.IsMacOS())
        {
            var script = $"tell application \"Music\" to set player position to {safePosition.ToString(CultureInfo.InvariantCulture)}";
            await RunAppleScriptAsync(script);
        }
        else
        {
            SetITunesProperty("PlayerPosition", safePosition);
        }

        _position = safePosition;
        ProgressChanged.Invoke(this, _position);
    }

    public Task<double> GetProgressAsync() => Task.FromResult(_position);

    public Task<List<Playlist>> GetPlaylistsAsync()
    {
        // Keep this empty for now. Local playback control is implemented first;
        // MusicKit/library browsing can be added later without changing the UI contract.
        return Task.FromResult(new List<Playlist>());
    }

    public Task SetPlaylistAsync(string playlistId) => Task.CompletedTask;

    public async Task SetShuffleAsync(bool enabled)
    {
        try
        {
            await EnsureConnectedAsync();

            if (OperatingSystem.IsMacOS())
            {
                var value = enabled ? "true" : "false";
                await RunAppleScriptAsync($"tell application \"Music\" to set shuffle enabled to {value}");
            }
            else
            {
                SetITunesProperty("ShuffleEnabled", enabled);
            }
        }
        catch
        {
            // Shuffle support varies across Music.app/iTunes versions.
            // Keep this non-fatal because SettingsViewModel fires it in the background.
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (!_connected)
            await LoginAsync();
    }

    private async Task SafePollAsync()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) == 1)
            return;

        try
        {
            var state = OperatingSystem.IsMacOS()
                ? await ReadMacStateAsync()
                : OperatingSystem.IsWindows()
                    ? ReadWindowsState()
                    : ApplePlaybackState.Unavailable("Apple Music local control is only supported on macOS and Windows.");

            ApplyState(state);
        }
        catch (Exception ex)
        {
            ApplyState(ApplePlaybackState.Unavailable(MapFriendlyError(ex)));
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    private void ApplyState(ApplePlaybackState state)
    {
        if (!state.IsAvailable)
        {
            SetUnavailable(state.ErrorMessage ?? NotDetectedMessage);
            return;
        }

        var oldTrackId = _current?.Id;
        var wasPlaying = _playing;

        _current = state.Track ?? Track.Empty;
        _position = Math.Max(0, state.PositionSeconds);
        _playing = state.IsPlaying;

        if (oldTrackId != _current.Id)
            TrackChanged.Invoke(this, _current);

        if (wasPlaying != _playing)
            PlaybackStateChanged.Invoke(this, _playing);

        ProgressChanged.Invoke(this, _position);
    }

    private void SetUnavailable(string message)
    {
        var wasPlaying = _playing;
        var previousId = _current?.Id;

        _playing = false;
        _position = 0;
        _current = new Track
        {
            Id = "apple-unavailable",
            Title = message,
            Artist = "Open Music.app on macOS or install iTunes on Windows.",
            Album = "",
            Duration = TimeSpan.Zero
        };

        if (previousId != _current.Id)
            TrackChanged.Invoke(this, _current);

        if (wasPlaying)
            PlaybackStateChanged.Invoke(this, false);

        ProgressChanged.Invoke(this, 0);
    }

    // ── macOS / AppleScript ──────────────────────────────────────────────────

    private static async Task<ApplePlaybackState> ReadMacStateAsync()
    {
        var script = $$"""
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
            return ApplePlaybackState.Unavailable(NotDetectedMessage);

        var parts = output.Split(Separator);
        if (parts.Length < 7 || parts[0].Equals("STOPPED", StringComparison.OrdinalIgnoreCase))
            return ApplePlaybackState.Available(Track.Empty, 0, false);

        var duration = ParseDouble(parts[4]);
        var position = ParseDouble(parts[5]);
        var stateText = parts[6];

        var track = new Track
        {
            Id = string.IsNullOrWhiteSpace(parts[0]) ? $"apple-{parts[1]}-{parts[2]}-{parts[3]}" : parts[0],
            Title = string.IsNullOrWhiteSpace(parts[1]) ? "Unknown Track" : parts[1],
            Artist = string.IsNullOrWhiteSpace(parts[2]) ? "Unknown Artist" : parts[2],
            Album = parts[3],
            Duration = TimeSpan.FromSeconds(Math.Max(0, duration))
        };

        return ApplePlaybackState.Available(track, position, stateText.Equals("playing", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> RunAppleScriptAsync(string script)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "osascript",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(NotDetectedMessage, ex);
        }

        await process.StandardInput.WriteAsync(script);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? NotDetectedMessage : error.Trim());

        return output.Trim();
    }

    // ── Windows / iTunes COM ─────────────────────────────────────────────────

    private ApplePlaybackState ReadWindowsState()
    {
        try
        {
            EnsureITunes();
            var app = _itunes!;
            var currentTrack = GetITunesProperty(app, "CurrentTrack");
            var playerState = Convert.ToInt32(GetITunesProperty(app, "PlayerState"), CultureInfo.InvariantCulture);
            var position = Convert.ToDouble(GetITunesProperty(app, "PlayerPosition") ?? 0, CultureInfo.InvariantCulture);
            var isPlaying = playerState == 1;

            if (currentTrack is null)
                return ApplePlaybackState.Available(Track.Empty, position, isPlaying);

            var title = Convert.ToString(TryGetITunesProperty(currentTrack, "Name"), CultureInfo.InvariantCulture) ?? "Unknown Track";
            var artist = Convert.ToString(TryGetITunesProperty(currentTrack, "Artist"), CultureInfo.InvariantCulture) ?? "Unknown Artist";
            var album = Convert.ToString(TryGetITunesProperty(currentTrack, "Album"), CultureInfo.InvariantCulture) ?? "";
            var duration = Convert.ToDouble(TryGetITunesProperty(currentTrack, "Duration") ?? 0, CultureInfo.InvariantCulture);
            var id = Convert.ToString(TryGetITunesProperty(currentTrack, "TrackDatabaseID"), CultureInfo.InvariantCulture);

            var track = new Track
            {
                Id = string.IsNullOrWhiteSpace(id) ? $"itunes-{title}-{artist}-{album}" : id,
                Title = string.IsNullOrWhiteSpace(title) ? "Unknown Track" : title,
                Artist = string.IsNullOrWhiteSpace(artist) ? "Unknown Artist" : artist,
                Album = album,
                Duration = TimeSpan.FromSeconds(Math.Max(0, duration))
            };

            return ApplePlaybackState.Available(track, position, isPlaying);
        }
        catch (Exception ex)
        {
            return ApplePlaybackState.Unavailable(MapFriendlyError(ex));
        }
    }

    private void EnsureITunes()
    {
        if (_itunes is not null)
            return;

        var type = Type.GetTypeFromProgID("iTunes.Application");
        if (type is null)
            throw new InvalidOperationException(NotDetectedMessage);

        _itunes = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(NotDetectedMessage);
    }

    private void InvokeITunes(string methodName, params object?[] args)
    {
        EnsureITunes();
        _itunes!.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod, null, _itunes, args);
    }

    private object? GetITunesProperty(string propertyName)
    {
        EnsureITunes();
        return GetITunesProperty(_itunes!, propertyName);
    }

    private static object? GetITunesProperty(object target, string propertyName)
    {
        return target.GetType().InvokeMember(propertyName, BindingFlags.GetProperty, null, target, null);
    }

    private static object? TryGetITunesProperty(object target, string propertyName)
    {
        try
        {
            return GetITunesProperty(target, propertyName);
        }
        catch
        {
            return null;
        }
    }

    private void SetITunesProperty(string propertyName, object value)
    {
        EnsureITunes();
        _itunes!.GetType().InvokeMember(propertyName, BindingFlags.SetProperty, null, _itunes, new[] { value });
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private static double ParseDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
            return invariant;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current))
            return current;

        return 0;
    }

    private static string MapFriendlyError(Exception ex)
    {
        var text = ex.ToString();

        if (text.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-1743", StringComparison.OrdinalIgnoreCase))
        {
            return "SyncDeck needs permission to control Music.app. Allow it in macOS System Settings > Privacy & Security > Automation.";
        }

        if (text.Contains("iTunes.Application", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("osascript", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Music", StringComparison.OrdinalIgnoreCase))
        {
            return NotDetectedMessage;
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? NotDetectedMessage : ex.Message;
    }

    public void Dispose()
    {
        _poll.Dispose();
    }

    private sealed record ApplePlaybackState(bool IsAvailable, Track? Track, double PositionSeconds, bool IsPlaying, string? ErrorMessage)
    {
        public static ApplePlaybackState Available(Track? track, double positionSeconds, bool isPlaying)
            => new(true, track, positionSeconds, isPlaying, null);

        public static ApplePlaybackState Unavailable(string message)
            => new(false, null, 0, false, message);
    }
}
