using System;
using System.Threading;

namespace SyncDeck.Utilities;

public static class SingleInstanceManager
{
    private const string MutexName = "Global\\SyncDeck_SingleInstance_A3F1";

    public static Mutex AcquireMutex(out bool createdNew)
    {
        var mutex = new Mutex(true, MutexName, out createdNew);
        return mutex;
    }

    public static void BringExistingToFront()
    {
        // On Windows a named pipe / WM_ACTIVATEAPP message would be ideal.
        // For now, the existing window will remain wherever it is.
        // TODO: implement cross-process window activation via named pipe.
        Console.WriteLine("SyncDeck is already running.");
    }
}
