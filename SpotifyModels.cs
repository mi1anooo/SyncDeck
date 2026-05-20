using SyncDeck.Models;
using SyncDeck.Services.AppleMusic;
using SyncDeck.Services.Spotify;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SyncDeck.Services.Music;

/// <summary>
/// Facade that the ViewModels interact with.
/// Holds a reference to whichever IMusicProvider is currently active and
/// forwards events from it, unsubscribing from the previous provider first.
/// </summary>
public class MusicService : IMusicService
{
    private readonly MockMusicProvider    _mock;
    private readonly SpotifyMusicProvider _spotify;
    private readonly AppleMusicProvider   _apple;

    private IMusicProvider _active;

    public IMusicProvider ActiveProvider => _active;

    public event EventHandler<Track?> TrackChanged        = delegate { };
    public event EventHandler<bool>   PlaybackStateChanged = delegate { };
    public event EventHandler<double> ProgressChanged      = delegate { };

    public MusicService(
        MockMusicProvider    mock,
        SpotifyMusicProvider spotify,
        AppleMusicProvider   apple)
    {
        _mock    = mock;
        _spotify = spotify;
        _apple   = apple;
        _active  = _mock;
        Subscribe(_active);
    }

    public void SwitchProvider(string providerName)
    {
        Unsubscribe(_active);
        _active = providerName switch
        {
            "Spotify"     => _spotify,
            "AppleMusic"  => _apple,
            _             => _mock,
        };
        Subscribe(_active);
    }

    // ── Event forwarding ──────────────────────────────────────────────────────

    private void Subscribe(IMusicProvider p)
    {
        p.TrackChanged         += OnTrackChanged;
        p.PlaybackStateChanged += OnPlaybackStateChanged;
        p.ProgressChanged      += OnProgressChanged;
    }

    private void Unsubscribe(IMusicProvider p)
    {
        p.TrackChanged         -= OnTrackChanged;
        p.PlaybackStateChanged -= OnPlaybackStateChanged;
        p.ProgressChanged      -= OnProgressChanged;
    }

    private void OnTrackChanged        (object? s, Track? t) => TrackChanged.Invoke(s, t);
    private void OnPlaybackStateChanged(object? s, bool   v) => PlaybackStateChanged.Invoke(s, v);
    private void OnProgressChanged     (object? s, double v) => ProgressChanged.Invoke(s, v);

    // ── Proxy to active provider ──────────────────────────────────────────────

    public Task<Track?>         GetCurrentTrackAsync()       => _active.GetCurrentTrackAsync();
    public Task                 PlayAsync()                   => _active.PlayAsync();
    public Task                 PauseAsync()                  => _active.PauseAsync();
    public Task                 NextAsync()                   => _active.NextAsync();
    public Task                 PreviousAsync()               => _active.PreviousAsync();
    public Task                 SeekAsync(double pos)         => _active.SeekAsync(pos);
    public Task<double>         GetProgressAsync()            => _active.GetProgressAsync();
    public Task<List<Playlist>> GetPlaylistsAsync()           => _active.GetPlaylistsAsync();
    public Task                 SetPlaylistAsync(string id)   => _active.SetPlaylistAsync(id);
    public Task                 SetShuffleAsync(bool enabled) => _active.SetShuffleAsync(enabled);
    public Task                 LoginAsync()                  => _active.LoginAsync();
    public Task                 LogoutAsync()                 => _active.LogoutAsync();
}
