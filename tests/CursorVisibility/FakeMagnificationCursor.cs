// The production service and helper are tested without changing the desktop cursor.
namespace PathOfWASD.Internals;

internal static class MagnificationCursor
{
    internal static bool Foreground, InitializeSucceeds = true, HideSucceeds = true;
    internal static int Initializes, Hides, Shows;
    internal static int LastVisibilityThread;
    internal static bool MagInitialize() { Initializes++; return InitializeSucceeds; }
    internal static bool MagUninitialize() => true;
    internal static bool MagShowSystemCursor(bool show)
    {
        LastVisibilityThread = Environment.CurrentManagedThreadId;
        if (show) { Shows++; return true; }
        Hides++;
        return HideSucceeds;
    }
    internal static bool IsPoeForeground() => Foreground;
    internal static void RestoreForRecovery() { Shows++; }
    internal static void Reset()
    {
        Foreground = true;
        InitializeSucceeds = HideSucceeds = true;
        Initializes = Hides = Shows = 0;
    }
}
