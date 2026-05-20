using SyncDeck.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace SyncDeck.Services.Music;

/// <summary>
/// Simulates music playback with hard-coded tracks.
/// No external service required — works offline for full UI testing.
/// </summary>
public class MockMusicProvider : IMusicProvider, IDisposable
{
    // ── Static library ────────────────────────────────────────────────────────

    private static readonly List<Track> Tracks = new()
    {
        new() { Id="1", Title="Midnight Frequency",          Artist="Neon Chrome",    Album="Static Dreams",   Duration=TimeSpan.FromSeconds(214) },
        new() { Id="2", Title="Cyber Velocity (Extended Mix)", Artist="AXIOM//DRIVE", Album="Protocol Zero",   Duration=TimeSpan.FromSeconds(387) },
        new() { Id="3", Title="Ghost in the Datastream",    Artist="Parallax Unit",   Album="Null Space",      Duration=TimeSpan.FromSeconds(263) },
        new() { Id="4", Title="Blue Signal",                 Artist="Terminal Echo",   Album="Carrier Wave",   Duration=TimeSpan.FromSeconds(198) },
        new() { Id="5", Title="Orbital Decay",               Artist="Neon Chrome",    Album="Static Dreams",   Duration=TimeSpan.FromSeconds(341) },
        new() { Id="6", Title="Synthwave Requiem",           Artist="AXIOM//DRIVE",   Album="Protocol Zero",   Duration=TimeSpan.FromSeconds(292) },
    };

    private static readonly List<Playlist> Playlists = new()
    {
        new() { Id="pl1", Name="Late Night Sessions",  TrackCount=12 },
        new() { Id="pl2", Name="Club Protocol",        TrackCount=24 },
        new() { Id="pl3", Name="Static Dreams",        TrackCount=8  },
    };

    // ── State ─────────────────────────────────────────────────────────────────

    private int    _index    = 0;
    private double _position = 0;
    private bool   _playing  = false;
    private bool   _shuffle  = false;

    private readonly System.Timers.Timer _timer = new(1000) { AutoReset = true };
    private readonly Random _rng    = new();

    // ── IMusicProvider ────────────────────────────────────────────────────────

    public string ProviderName   => "Mock";
    public bool   IsAuthenticated => true;
    public bool   IsPlaying      => _playing;

    public event EventHandler<Track?> TrackChanged         = delegate { };
    public event EventHandler<bool>   PlaybackStateChanged = delegate { };
    public event EventHandler<double> ProgressChanged      = delegate { };

    public MockMusicProvider()
    {
        _timer.Elapsed += OnTick;
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        if (!_playing) return;
        _position += 1.0;
        if (_position >= Tracks[_index].Duration.TotalSeconds)
        {
            _ = NextAsync();
            return;
        }
        ProgressChanged.Invoke(this, _position);
    }

    public Task<Track?> GetCurrentTrackAsync()
        => Task.FromResult<Track?>(Tracks[_index]);

    public Task PlayAsync()
    {
        _playing = true;
        _timer.Start();
        PlaybackStateChanged.Invoke(this, true);
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        _playing = false;
        _timer.Stop();
        PlaybackStateChanged.Invoke(this, false);
        return Task.CompletedTask;
    }

    public async Task NextAsync()
    {
        _index    = _shuffle
            ? _rng.Next(Tracks.Count)
            : (_index + 1) % Tracks.Count;
        _position = 0;
        TrackChanged.Invoke(this, Tracks[_index]);
        if (_playing) await PlayAsync();
    }

    public async Task PreviousAsync()
    {
        // Restart current track if >3 s in; otherwise go back
        if (_position > 3.0)
        {
            _position = 0;
            ProgressChanged.Invoke(this, 0);
        }
        else
        {
            _index    = (_index - 1 + Tracks.Count) % Tracks.Count;
            _position = 0;
            TrackChanged.Invoke(this, Tracks[_index]);
        }
        if (_playing) await PlayAsync();
    }

    public Task SeekAsync(double positionSeconds)
    {
        _position = Math.Clamp(positionSeconds, 0, Tracks[_index].Duration.TotalSeconds);
        ProgressChanged.Invoke(this, _position);
        return Task.CompletedTask;
    }

    public Task<double>         GetProgressAsync()            => Task.FromResult(_position);
    public Task<List<Playlist>> GetPlaylistsAsync()           => Task.FromResult(Playlists);
    public Task                 SetPlaylistAsync(string id)   => Task.CompletedTask;
    public Task                 SetShuffleAsync(bool enabled) { _shuffle = enabled; return Task.CompletedTask; }
    public Task                 LoginAsync()                  => Task.CompletedTask;
    public Task                 LogoutAsync()                 => Task.CompletedTask;

    public void Dispose() => _timer.Dispose();
}
