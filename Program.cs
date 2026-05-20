using Avalonia;
using SyncDeck.Utilities;
using System;
using System.IO;
using System.Threading;

namespace SyncDeck;

sealed class Program
{
    // Keep mutex alive for the lifetime of the process
    private static Mutex? _instanceMutex;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            _instanceMutex = SingleInstanceManager.AcquireMutex(out bool isFirst);

            if (!isFirst)
            {
                SingleInstanceManager.BringExistingToFront();
                _instanceMutex?.Dispose();
                return 0;
            }

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                return 0;
            }
            finally
            {
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            return 1;
        }
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SyncDeck");

            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "crash.log"), ex.ToString());
        }
        catch
        {
            // Nothing else to do. Avoid crashing while writing crash details.
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
