using SyncDeck.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SyncDeck.Services.Music;

public interface IMusicService
{
    IMusicProvider ActiveProvider { get; }

    // Proxied events (forwarded from active provider)
    event EventHandler<Track?> TrackChanged;
    event EventHandler<bool>   PlaybackStateChanged;
    event EventHandler<double> ProgressChanged;

    void SwitchProvider(string providerName);

    Task<Track?>         GetCurrentTrackAsync();
    Task                 PlayAsync();
    Task                 PauseAsync();
    Task                 NextAsync();
    Task                 PreviousAsync();
    Task                 SeekAsync(double positionSeconds);
    Task<double>         GetProgressAsync();
    Task<List<Playlist>> GetPlaylistsAsync();
    Task                 SetPlaylistAsync(string id);
    Task                 SetShuffleAsync(bool enabled);
    Task                 LoginAsync();
    Task                 LogoutAsync();
}
