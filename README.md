# SyncDeck

> Y2K Futurist desktop music player — Sony MiniDisc meets early-2000s cyber aesthetic.  
> Built with C# · Avalonia UI · MVVM · .NET 8

---

## Contents

- [Quick Start](#quick-start)
- [Build — Windows](#build--windows)
- [Build — macOS](#build--macos)
- [Project Structure](#project-structure)
- [Architecture Overview](#architecture-overview)
- [Spotify Setup (Milestone 2)](#spotify-setup-milestone-2)
- [Apple Music Notes (Milestone 3)](#apple-music-notes-milestone-3)
- [Theme System](#theme-system)
- [Roadmap](#roadmap)

---

## Quick Start

### Prerequisites

| Tool        | Version  |
|-------------|----------|
| .NET SDK    | 8.0 +    |
| Git         | any      |

```bash
# 1. Clone
git clone https://github.com/you/SyncDeck.git
cd SyncDeck

# 2. Restore packages
dotnet restore

# 3. Run (Mock provider — no external accounts needed)
dotnet run
```

The app launches with **Mock** playback active.  
Hit **Play** and all 6 sample tracks will play immediately.

---

## Build — Windows

### Development run
```powershell
dotnet run --project SyncDeck.csproj
```

### Self-contained publish (single .exe)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o ./publish/windows
```
Produces `publish/windows/SyncDeck.exe` — copy anywhere, no .NET install required.

### Windows installer (optional — requires Inno Setup)
```powershell
# After publish step above, run:
iscc setup/SyncDeck.iss
```

---

## Build — macOS

### Development run
```bash
dotnet run --project SyncDeck.csproj
```

### Self-contained publish (.app bundle)
```bash
dotnet publish -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/macos

# Wrap in .app (optional)
mkdir -p "SyncDeck.app/Contents/MacOS"
cp publish/macos/SyncDeck "SyncDeck.app/Contents/MacOS/"
# Add Info.plist from setup/macos/Info.plist
```

> **Note:** macOS Gatekeeper may block unsigned builds.  
> To bypass during dev: `xattr -cr SyncDeck.app`

---

## Project Structure

```
SyncDeck/
├── Models/
│   ├── Track.cs              # Single playable track
│   ├── Playlist.cs           # Playlist metadata
│   └── AppSettings.cs        # Persisted user preferences
│
├── ViewModels/
│   ├── MainViewModel.cs      # Primary UI state & commands
│   └── SettingsViewModel.cs  # Settings panel state & commands
│
├── Views/
│   ├── MainWindow.axaml(.cs) # Frameless custom window
│   ├── Controls/
│   │   ├── TitleBarControl   # Draggable chrome with custom buttons
│   │   ├── VisualizerControl # Animated disc/bars/waveform/minidisc
│   │   ├── TrackInfoControl  # Marquee title, artist, album art
│   │   └── TransportControl  # Play/pause/prev/next + progress bar
│   └── Panels/
│       └── SettingsPanel     # Floating settings overlay
│
├── Services/
│   ├── Music/
│   │   ├── IMusicProvider.cs # Interface all providers implement
│   │   ├── IMusicService.cs  # Facade the UI binds to
│   │   ├── MusicService.cs   # Switches active provider, forwards events
│   │   └── MockMusicProvider.cs  # Fully working fake playback
│   ├── Spotify/
│   │   ├── SpotifyAuthService.cs     # PKCE OAuth 2.0 flow
│   │   ├── SpotifyMusicProvider.cs   # Spotify Web API integration
│   │   └── SpotifyModels.cs          # JSON deserialization records
│   └── AppleMusic/
│       └── AppleMusicProvider.cs     # Documented stub — see notes
│
├── Themes/
│   ├── ThemeDefinition.cs    # Full theme data model
│   └── ThemeManager.cs       # Registry + live resource applicator
│
├── Utilities/
│   ├── SettingsService.cs    # JSON persistence (AppData)
│   ├── SingleInstanceManager.cs  # Mutex-based single instance
│   └── TimeFormatter.cs      # "3:47" formatting helper
│
├── Assets/
│   ├── Themes/               # Future per-theme image assets
│   ├── Icons/                # App icon
│   ├── Frames/               # Future decorative frame images
│   └── MockAlbumArt/         # Future static test art
│
└── App.axaml(.cs)            # DI container, app bootstrap
```

---

## Architecture Overview

### Provider pattern
All music sources implement `IMusicProvider`. The `MusicService` facade sits
between providers and the UI — it holds the active provider, forwards events,
and swaps cleanly when the user changes source. The UI never touches providers
directly.

```
MainViewModel ─── IMusicService (MusicService)
                        │
              ┌─────────┼──────────┐
       MockProvider  SpotifyProvider  AppleProvider
```

### Theme system
`ThemeManager` holds 4 `ThemeDefinition` objects. Calling `ApplyTheme(id)`
atomically overwrites all `ThemeX` keys in `Application.Current.Resources`.
Every control binds to `{DynamicResource ThemeX}` — changes propagate
instantly without reloading or freezing.

### MVVM
- `ObservableObject` + `[ObservableProperty]` from CommunityToolkit.Mvvm
- `[RelayCommand]` for async commands with automatic CanExecute
- No code in ViewModels that touches Avalonia types directly

---

## Spotify Setup (Milestone 2)

1. **Create a Spotify app**  
   Go to https://developer.spotify.com/dashboard → Create App  
   - Redirect URI: `http://localhost:5543/callback`  
   - Copy your **Client ID**

2. **Set your Client ID** (do NOT commit this to git)

   Option A — environment variable (recommended):
   ```powershell
   # Windows PowerShell
   $env:SPOTIFY_CLIENT_ID = "your_client_id_here"
   dotnet run
   ```
   ```bash
   # macOS / Linux
   export SPOTIFY_CLIENT_ID="your_client_id_here"
   dotnet run
   ```

   Option B — local config file (add to .gitignore):
   Create `appsettings.local.json`:
   ```json
   { "SpotifyClientId": "your_client_id_here" }
   ```

3. **Login flow**  
   Open Settings → select **Spotify** → click **Log in to Spotify**  
   Your browser will open Spotify's login page.  
   After authorizing, return to SyncDeck — it connects automatically.

### Spotify API Limitations

| Feature                    | Status          |
|----------------------------|-----------------|
| See currently playing track| ✅ Supported    |
| Skip / previous            | ✅ Supported    |
| Seek                       | ✅ Supported    |
| Play / Pause               | ✅ (Premium only) |
| Control volume             | ✅ (Premium only) |
| Stream audio directly      | ❌ Not supported (DRM) |
| Work without active device | ❌ Must have Spotify open |

> Spotify's Web API is a **remote control** — it cannot play audio itself.  
> The user must have Spotify open on at least one device (phone, desktop app, etc.)

---

## Apple Music Notes (Milestone 3)

Apple Music does not offer a cross-platform playback-control API equivalent to
Spotify's Web API. Current options per platform:

| Platform | Approach | Status in SyncDeck |
|----------|----------|-------------------|
| macOS    | AppleScript → `osascript` → Music.app | TODO in `AppleMusicProvider.cs` |
| Windows  | iTunes COM interop | TODO in `AppleMusicProvider.cs` |
| Cross-platform | MusicKit JS (browser only) | Not applicable |

The `AppleMusicProvider` class is a documented stub. The interface is wired
so switching to "Apple Music" in settings works — it just shows "Not implemented"
errors. A future contributor can add the platform-specific backends inside the
provider without changing any other code.

---

## Theme System

Four built-in themes:

| ID              | Feel                              | Visualizer |
|-----------------|-----------------------------------|------------|
| `SonyChrome`    | Gunmetal + blue LCD               | MiniDisc   |
| `CyberTribal`   | Dark graphite + violet glow       | Spinning disc |
| `EurotrashClub` | Black + acid green, rave UI       | Bars       |
| `FrostedBlue`   | Translucent blue plastic          | Waveform   |

To add a new theme: add a `ThemeDefinition` to `ThemeManager.All`. No XAML
changes required.

---

## Roadmap

### After MVP

1. **Real album art** — load images from Spotify's CDN or local files via `Bitmap`
2. **System tray** — minimize to tray, restore from tray icon
3. **Media key support** — `GlobalHotKey` for play/pause/skip from keyboard
4. **Apple Music macOS** — `osascript` bridge for Music.app control
5. **Apple Music Windows** — iTunes COM interop
6. **Volume control** — slider in transport or settings
7. **Last.fm scrobbling** — passive track reporting
8. **Custom frame images** — per-theme SVG/PNG decorative borders
9. **Audio visualization** — real FFT bars when playing local files
10. **Lyrics overlay** — Spotify lyrics API (requires partner access)

---

## .gitignore additions

```gitignore
appsettings.local.json
*.user
publish/
.vs/
bin/
obj/
```

---

*SyncDeck — Powered by Avalonia UI. No Electron. No webview. Pure native.*
