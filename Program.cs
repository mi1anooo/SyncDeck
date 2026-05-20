using Avalonia;
using SyncDeck.Utilities;
using System;
using System.Threading;

namespace SyncDeck;

sealed class Program
{
    // Keep mutex alive for the lifetime of the process
    private static Mutex? _instanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        _instanceMutex = SingleInstanceManager.AcquireMutex(out bool isFirst);

        if (!isFirst)
        {
            SingleInstanceManager.BringExistingToFront();
            _instanceMutex?.Dispose();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
