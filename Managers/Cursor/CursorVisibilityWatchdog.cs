using System.IO;
using PathOfWASD.Internals;

namespace PathOfWASD.Managers.Cursor;

internal static class CursorVisibilityWatchdog
{
    internal const string Argument = "--cursor-visibility-watchdog";
    internal static string MarkerName(int pid) => $"Local\\PathOfWASD.CursorRecovery.{pid}";

    internal static void Run()
    {
        using var marker = new Mutex(false, MarkerName(Environment.ProcessId));
        using var input = new StreamReader(Console.OpenStandardInput());
        using var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Monitor(input, output, TimeSpan.FromSeconds(2), MagnificationCursor.RestoreForRecovery);
    }

    // The UI lease expires even if the UI hangs. Runs before WPF and single-instance startup.
    internal static void Monitor(TextReader input, TextWriter output, TimeSpan timeout, Action restore)
    {
        bool mayBeHidden = false;
        try
        {
            output.WriteLine("ready");
            output.Flush();
            while (true)
            {
                var command = Task.Run(input.ReadLine).WaitAsync(timeout).GetAwaiter().GetResult();
                if (command == null) break;
                if (command is not ("hidden" or "visible")) break;
                mayBeHidden = command == "hidden";
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            System.Diagnostics.Trace.TraceWarning(ex.Message);
        }
        finally
        {
            if (mayBeHidden) restore();
        }
    }
}
