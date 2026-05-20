using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SyncDeck.Services.AppleMusic;
using SyncDeck.Services.Music;
using SyncDeck.Services.Spotify;
using SyncDeck.Themes;
using SyncDeck.ViewModels;
using SyncDeck.Views;
using System;

namespace SyncDeck;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Apply default theme before window is shown
            var themeManager = Services.GetRequiredService<ThemeManager>();
            themeManager.ApplyTheme("SonyChrome");

            var mainVm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Singletons — order matters for DI graph
        services.AddSingleton<ThemeManager>();
        services.AddSingleton<MockMusicProvider>();
        services.AddSingleton<SpotifyMusicProvider>();
        services.AddSingleton<AppleMusicProvider>();
        services.AddSingleton<IMusicService, MusicService>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
