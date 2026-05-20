using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncDeck.Models;
using SyncDeck.Services.Music;
using SyncDeck.Themes;
using SyncDeck.Utilities;
using System;
using System.Threading.Tasks;

namespace SyncDeck.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMusicService _music;
    private readonly ThemeManager  _theme;

    // ── Track info ────────────────────────────────────────────────────────────

    [ObservableProperty] private string _trackTitle   = "No Track";
    [ObservableProperty] private string _artist       = "─ ─ ─";
    [ObservableProperty] private string _albumName    = "";
    [ObservableProperty] private byte[]? _albumArtData;

    // ── Playback state ────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isPlaying;
    [ObservableProperty] private double _position;          // seconds
    [ObservableProperty] private double _duration = 1.0;   // seconds (never 0 — avoids divide-by-zero)
    [ObservableProperty] private string _elapsedText  = "0:00";
    [ObservableProperty] private string _durationText = "0:00";

    // ── UI flags ──────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isSettingsOpen;
    [ObservableProperty] private bool   _isSeeking;          // true while user drags slider
    [ObservableProperty] private bool   _trackChanging;      // triggers transition animation
    [ObservableProperty] private string _statusMessage = "";

    public SettingsViewModel Settings { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainViewModel(IMusicService music, ThemeManager theme, SettingsViewModel settings)
    {
        _music    = music;
        _theme    = theme;
        Settings  = settings;

        _music.TrackChanged         += OnTrackChanged;
        _music.PlaybackStateChanged += OnPlaybackStateChanged;
        _music.ProgressChanged      += OnProgressChanged;

        _ = LoadCurrentTrackAsync();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        try
        {
            if (IsPlaying) await _music.PauseAsync();
            else           await _music.PlayAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        try   { await _music.NextAsync(); }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        try   { await _music.PreviousAsync(); }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    /// Called by the progress bar when the user starts dragging.
    public void BeginSeek() => IsSeeking = true;

    /// Called by the progress bar when the user releases the thumb.
    public async Task EndSeekAsync(double seconds)
    {
        IsSeeking = false;
        try   { await _music.SeekAsync(seconds); }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    // ── Event handlers (arrive on background thread → marshal to UI) ──────────

    private void OnTrackChanged(object? _, Track? track)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            TrackChanging = true;
            await Task.Delay(180); // let animation start
            ApplyTrack(track);
            await Task.Delay(20);
            TrackChanging = false;
        });
    }

    private void OnPlaybackStateChanged(object? _, bool playing)
        => Dispatcher.UIThread.Post(() => IsPlaying = playing);

    private void OnProgressChanged(object? _, double seconds)
    {
        if (IsSeeking) return; // don't interrupt user drag
        Dispatcher.UIThread.Post(() =>
        {
            Position    = seconds;
            ElapsedText = TimeFormatter.Format(seconds);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task LoadCurrentTrackAsync()
    {
        try
        {
            var track = await _music.GetCurrentTrackAsync();
            ApplyTrack(track);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ApplyTrack(Track.Empty);
        }
    }

    private void ApplyTrack(Track? track)
    {
        track ??= Track.Empty;

        TrackTitle   = track.Title;
        Artist       = track.Artist;
        AlbumName    = track.Album;
        AlbumArtData = track.AlbumArtData;
        Duration     = Math.Max(1.0, track.Duration.TotalSeconds);
        DurationText = TimeFormatter.Format(Duration);
        Position     = 0;
        ElapsedText  = "0:00";
    }
}
