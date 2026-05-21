using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncDeck.Models;
using SyncDeck.Services.Music;
using SyncDeck.Themes;
using SyncDeck.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SyncDeck.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IMusicService _music;
    private readonly ThemeManager  _theme;
    private readonly AppSettings   _persisted;

    // ── Themes ────────────────────────────────────────────────────────────────

    public ObservableCollection<ThemeDefinition> Themes { get; } = new(ThemeManager.All);

    [ObservableProperty] private ThemeDefinition _selectedTheme = ThemeManager.All[0];

    partial void OnSelectedThemeChanged(ThemeDefinition value)
    {
        _theme.ApplyTheme(value.Id);
        _persisted.CurrentTheme = value.Id;
        SettingsService.Save(_persisted);
    }

    // ── Source ────────────────────────────────────────────────────────────────

    public ObservableCollection<string> Sources { get; } = new() { "Mock", "Spotify", "Apple Music" };

    public ObservableCollection<double> PlaybackSpeeds { get; } = new() { 33.0, 45.0 };

    [ObservableProperty] private string _selectedSource = "Mock";

    partial void OnSelectedSourceChanged(string value)
    {
        var key = value.Replace(" ", "");
        _music.SwitchProvider(key);
        _persisted.CurrentProvider = key;
        SettingsService.Save(_persisted);
        PlaylistStatusText = "Scanning playlists...";
        _ = RefreshPlaylistsAsync();
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isSpotifyLoggedIn;
    [ObservableProperty] private string _spotifyStatusText = "Log in to Spotify";
    [ObservableProperty] private string _appleStatusText   = "Connect Apple Music";

    [RelayCommand]
    private async Task SpotifyLoginAsync()
    {
        ForceProvider("Spotify", "Spotify");

        try
        {
            if (IsSpotifyLoggedIn)
            {
                await _music.LogoutAsync();
                IsSpotifyLoggedIn = false;
                SpotifyStatusText = "Log in to Spotify";
            }
            else
            {
                await _music.LoginAsync();
                IsSpotifyLoggedIn = true;
                SpotifyStatusText = "Log out of Spotify";
            }
        }
        catch (Exception ex) { SpotifyStatusText = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task AppleMusicConnectAsync()
    {
        ForceProvider("Apple Music", "AppleMusic");

        try
        {
            await _music.LoginAsync();
            AppleStatusText = OperatingSystem.IsMacOS()
                ? "Apple Music connected"
                : "iTunes connected";
            await RefreshPlaylistsAsync();
        }
        catch (Exception ex)
        {
            AppleStatusText = ex.Message;
        }
    }

    private void ForceProvider(string displayName, string providerKey)
    {
        if (SelectedSource != displayName)
        {
            SelectedSource = displayName;
        }
        else
        {
            _music.SwitchProvider(providerKey);
            _persisted.CurrentProvider = providerKey;
            SettingsService.Save(_persisted);
        }
    }

    // ── Playlists ─────────────────────────────────────────────────────────────

    public ObservableCollection<Playlist> Playlists { get; } = new();

    private bool _suppressPlaylistChange;

    [ObservableProperty] private Playlist? _selectedPlaylist;
    [ObservableProperty] private bool _hasPlaylists;
    [ObservableProperty] private string _playlistStatusText = "Connect Apple Music/iTunes or Spotify, then refresh playlists.";

    partial void OnSelectedPlaylistChanged(Playlist? value)
    {
        if (_suppressPlaylistChange || value is null) return;
        _ = SelectPlaylistAsync(value);
    }

    private async Task SelectPlaylistAsync(Playlist playlist)
    {
        try
        {
            PlaylistStatusText = $"Loading {playlist.Name}...";
            await _music.SetPlaylistAsync(playlist.Id);
            _persisted.LastPlaylistId = playlist.Id;
            SettingsService.Save(_persisted);
            PlaylistStatusText = $"Playing {playlist.Name}";
        }
        catch (Exception ex)
        {
            PlaylistStatusText = ex.Message;
        }
    }

    // ── Turntable playback speed ─────────────────────────────────────────────

    [ObservableProperty] private double _playbackRpm = 33.0;

    partial void OnPlaybackRpmChanged(double value)
    {
        _persisted.PlaybackRpm = value;
        SettingsService.Save(_persisted);
    }

    // ── Toggles ───────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _shuffle;

    partial void OnShuffleChanged(bool value)
    {
        _ = _music.SetShuffleAsync(value);
        _persisted.Shuffle = value;
        SettingsService.Save(_persisted);
    }

    [ObservableProperty] private bool _alwaysOnTop;

    partial void OnAlwaysOnTopChanged(bool value)
    {
        _persisted.AlwaysOnTop = value;
        SettingsService.Save(_persisted);
        // Applied to window in MainWindow.axaml.cs via event binding
        AlwaysOnTopChanged?.Invoke(this, value);
    }

    [ObservableProperty] private bool _transparent;

    partial void OnTransparentChanged(bool value)
    {
        _persisted.Transparent = value;
        SettingsService.Save(_persisted);
        TransparencyChanged?.Invoke(this, value);
    }

    // Events for the window to subscribe to
    public event EventHandler<bool>? AlwaysOnTopChanged;
    public event EventHandler<bool>? TransparencyChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsViewModel(IMusicService music, ThemeManager theme)
    {
        _music    = music;
        _theme    = theme;
        _persisted = SettingsService.Load();

        // Restore persisted values
        var allList = new System.Collections.Generic.List<ThemeDefinition>(ThemeManager.All);
        var savedTheme = allList.Find(t => t.Id == _persisted.CurrentTheme)
                         ?? ThemeManager.All[0];
        _selectedTheme = savedTheme;

        _selectedSource = _persisted.CurrentProvider switch
        {
            "Spotify"    => "Spotify",
            "AppleMusic" => "Apple Music",
            _            => "Mock"
        };

        _alwaysOnTop = _persisted.AlwaysOnTop;
        _transparent = _persisted.Transparent;
        _playbackRpm = Math.Abs(_persisted.PlaybackRpm - 45.0) < 0.1 ? 45.0 : 33.0;
        _shuffle     = _persisted.Shuffle;

        _music.SwitchProvider(_persisted.CurrentProvider);

        _ = RefreshPlaylistsAsync();
    }

    [RelayCommand]
    private async Task RefreshPlaylistsAsync()
    {
        try
        {
            PlaylistStatusText = "Scanning playlists...";
            var lists = await _music.GetPlaylistsAsync();

            Dispatcher.UIThread.Post(() =>
            {
                _suppressPlaylistChange = true;
                try
                {
                    var previousSelection = SelectedPlaylist?.Id ?? _persisted.LastPlaylistId;

                    Playlists.Clear();
                    foreach (var p in lists) Playlists.Add(p);

                    HasPlaylists = Playlists.Count > 0;
                    SelectedPlaylist = null;

                    if (HasPlaylists)
                    {
                        foreach (var playlist in Playlists)
                        {
                            if (playlist.Id == previousSelection)
                            {
                                SelectedPlaylist = playlist;
                                break;
                            }
                        }

                        SelectedPlaylist ??= Playlists[0];
                        PlaylistStatusText = $"{Playlists.Count} playlist{(Playlists.Count == 1 ? "" : "s")} found";
                    }
                    else
                    {
                        PlaylistStatusText = SelectedSource == "Apple Music"
                            ? "No local playlists found. Open iTunes/Music and make sure Sync Library is enabled."
                            : "No playlists found for this source.";
                    }
                }
                finally
                {
                    _suppressPlaylistChange = false;
                }
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                HasPlaylists = false;
                PlaylistStatusText = ex.Message;
            });
        }
    }

    [RelayCommand]
    private void ResetTheme()
    {
        SelectedTheme = ThemeManager.All[0];
    }
}
