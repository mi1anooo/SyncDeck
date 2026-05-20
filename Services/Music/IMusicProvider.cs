using SyncDeck.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SyncDeck.Services.Music;

/// <summary>
/// Core contract for all music source providers.
/// Implementations: MockMusicProvider, SpotifyMusicProvider, AppleMusicProvider.
/// </summary>
public interface IMusicProvider
{
    // ── Identity ──────────────────────────────────────────────────────────────
    string ProviderName  { get; }
    bool   IsAuthenticated { get; }
    bool   IsPlaying     { get; }

    // ── Events ─────────────────────────────────────────────────────────────────
    /// Fired when the active track changes (skip, auto-advance, etc.)
    event EventHandler<Track?> TrackChanged;
    /// Fired when play/pause state changes. bool = isPlaying.
    event EventHandler<bool>   PlaybackStateChanged;
    /// Fired approximately every second during playback. double = position in seconds.
    event EventHandler<double> ProgressChanged;

    // ── Playback control ───────────────────────────────────────────────────────
    Task<Track?> GetCurrentTrackAsync();
    Task         PlayAsync();
    Task         PauseAsync();
    Task         NextAsync();
    Task         PreviousAsync();
    Task         SeekAsync(double positionSeconds);
    Task<double> GetProgressAsync();

    // ── Library ────────────────────────────────────────────────────────────────
    Task<List<Playlist>> GetPlaylistsAsync();
    Task                 SetPlaylistAsync(string playlistId);
    Task                 SetShuffleAsync(bool enabled);

    // ── Auth ───────────────────────────────────────────────────────────────────
    Task LoginAsync();
    Task LogoutAsync();
}
