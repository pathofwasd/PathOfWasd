using System.Runtime.InteropServices;
using System.Text;

namespace PathOfWASD.Internals;

internal static class MagnificationCursor
{
    [DllImport("Magnification.dll")]
    internal static extern bool MagInitialize();
    [DllImport("Magnification.dll")]
    internal static extern bool MagUninitialize();
    [DllImport("Magnification.dll")]
    internal static extern bool MagShowSystemCursor(bool show);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder text, int count);

    // Read window metadata only. No process access, input attachment or activation.
    internal static bool IsPoeForeground()
    {
        var window = GetForegroundWindow();
        var title = new StringBuilder(256);
        var name = new StringBuilder(256);
        return window != IntPtr.Zero
            && GetWindowText(window, title, title.Capacity) != 0
            && GetClassName(window, name, name.Capacity) != 0
            && title.ToString() == "Path of Exile"
            && name.ToString() == "POEWindowClass";
    }

    internal static void RestoreForRecovery()
    {
        try
        {
            if (!MagInitialize()) return;
            try { MagShowSystemCursor(true); }
            finally { MagUninitialize(); }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            System.Diagnostics.Trace.TraceError(ex.Message);
        }
    }
}
