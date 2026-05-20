using SyncDeck.Models;
using SyncDeck.Services.Music;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SyncDeck.Services.AppleMusic;

/// <summary>
/// Apple Music provider stub.
///
/// APPLE MUSIC DESKTOP LIMITATIONS (as of 2024/2025):
/// ─────────────────────────────────────────────────────────────────────────────
/// Apple does NOT provide a public cross-platform Web API for playback control
/// comparable to Spotify's Web API. The options are:
///
///   1. MusicKit JS (browser only) — not usable in a native desktop app.
///
///   2. MusicKit on macOS (Swift/Objective-C only) — macOS-exclusive native
///      framework. Would require a separate macOS-native service process and
///      IPC bridge, significantly increasing complexity.
///
///   3. Apple Music API (server-to-server, developer token) — read-only catalogue
///      search and library access. Does NOT support playback control.
///
///   4. AppleScript / osascript on macOS — controls the local Music.app process.
///      macOS-only, fragile, not cross-platform.
///
///   5. iTunes COM API on Windows — controls the local iTunes process via COM.
///      Windows-only, requires iTunes to be installed.
///
/// Decision: This provider is a documented stub. The interface is fully wired
/// so the theme system, settings UI, and provider switch all work. A future
/// contributor can implement the macOS (AppleScript bridge) or Windows
/// (iTunes COM) backend without changing any other code.
///
/// TODO — macOS path: Use Process.Start("osascript", ...) to control Music.app.
/// TODO — Windows path: Use COM interop with iTunes.Application.
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
public class AppleMusicProvider : IMusicProvider
{
    public string ProviderName    => "Apple Music";
    public bool   IsAuthenticated => false;
    public bool   IsPlaying       => false;

    public event EventHandler<Track?>  TrackChanged         = delegate { };
    public event EventHandler<bool>    PlaybackStateChanged = delegate { };
    public event EventHandler<double>  ProgressChanged      = delegate { };

    private static Task NotSupported(string feature)
        => Task.FromException(new NotSupportedException(
            $"Apple Music: '{feature}' is not yet implemented on this platform. " +
            $"See AppleMusicProvider.cs for details and TODOs."));

    public Task LoginAsync()
        => Task.FromException(new NotSupportedException(
            "Apple Music authentication is not yet implemented. " +
            "On macOS, Music.app handles its own session — no login required through SyncDeck. " +
            "On Windows, iTunes must be running."));

    public Task LogoutAsync()           => Task.CompletedTask;
    public Task<Track?>   GetCurrentTrackAsync()  => Task.FromResult<Track?>(null);
    public Task           PlayAsync()             => NotSupported("Play");
    public Task           PauseAsync()            => NotSupported("Pause");
    public Task           NextAsync()             => NotSupported("Next");
    public Task           PreviousAsync()         => NotSupported("Previous");
    public Task           SeekAsync(double p)     => NotSupported("Seek");
    public Task<double>   GetProgressAsync()      => Task.FromResult(0.0);
    public Task<List<Playlist>> GetPlaylistsAsync()    => Task.FromResult(new List<Playlist>());
    public Task           SetPlaylistAsync(string id)  => NotSupported("SetPlaylist");
    public Task           SetShuffleAsync(bool e)      => NotSupported("SetShuffle");
}
