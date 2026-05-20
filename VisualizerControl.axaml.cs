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
        => _theme.ApplyTheme(value.Id);

    // ── Source ────────────────────────────────────────────────────────────────

    public ObservableCollection<string> Sources { get; } = new() { "Mock", "Spotify", "Apple Music" };

    [ObservableProperty] private string _selectedSource = "Mock";

    partial void OnSelectedSourceChanged(string value)
    {
        var key = value.Replace(" ", "");
        _music.SwitchProvider(key);
        _persisted.CurrentProvider = key;
        SettingsService.Save(_persisted);
        _ = RefreshPlaylistsAsync();
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isSpotifyLoggedIn;
    [ObservableProperty] private string _spotifyStatusText = "Log in to Spotify";
    [ObservableProperty] private string _appleStatusText   = "Connect Apple Music";

    [RelayCommand]
    private async Task SpotifyLoginAsync()
    {
        try
        {
            if (IsSpotifyLoggedIn)
            {
                await _music.LogoutAsync();
                IsSpotifyLoggedIn  = false;
                SpotifyStatusText  = "Log in to Spotify";
            }
            else
            {
                await _music.LoginAsync();
                IsSpotifyLoggedIn  = true;
                SpotifyStatusText  = "Log out of Spotify";
            }
        }
        catch (Exception ex) { SpotifyStatusText = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private Task AppleMusicConnectAsync()
    {
        AppleStatusText = "Apple Music: see README for platform notes.";
        return Task.CompletedTask;
    }

    // ── Playlists ─────────────────────────────────────────────────────────────

    public ObservableCollection<Playlist> Playlists { get; } = new();

    [ObservableProperty] private Playlist? _selectedPlaylist;

    partial void OnSelectedPlaylistChanged(Playlist? value)
    {
        if (value is null) return;
        _ = _music.SetPlaylistAsync(value.Id);
        _persisted.LastPlaylistId = value.Id;
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
        _shuffle     = _persisted.Shuffle;

        _ = RefreshPlaylistsAsync();
    }

    [RelayCommand]
    private async Task RefreshPlaylistsAsync()
    {
        try
        {
            var lists = await _music.GetPlaylistsAsync();
            Dispatcher.UIThread.Post(() =>
            {
                Playlists.Clear();
                foreach (var p in lists) Playlists.Add(p);
                if (Playlists.Count > 0) SelectedPlaylist = Playlists[0];
            });
        }
        catch { /* provider may not support playlists */ }
    }

    [RelayCommand]
    private void ResetTheme()
    {
        SelectedTheme = ThemeManager.All[0];
    }
}
