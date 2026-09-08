using System.Runtime.InteropServices;
using PathOfWASD.Internals;
using PathOfWASD.Overlays.Cursor;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            // Only this test's own transparent windows. No hooks, input injection,
            // cursor visibility changes, game access, or user settings.
            using var current = new NativeCursorWindow();
            using var baseline = new NativeCursorWindow();
            current.MoveTo(-120, -140);
            LegacyMoveTo(baseline, -120, -140);
            Check(!current.IsHandleCreated && !baseline.IsHandleCreated, "position before handle creation stays lazy");
            Check(current.Bounds == baseline.Bounds, "pre-handle bounds match baseline");

            _ = current.Handle;
            _ = baseline.Handle;
            using var currentMonitor = new PositionMonitor(current.Handle);
            using var baselineMonitor = new PositionMonitor(baseline.Handle);
            for (var i = 0; i < 200; i++)
            {
                var x = -200 - i % 2 * 70;
                var y = -210 - i % 2 * 80;
                current.MoveTo(x, y);
                LegacyMoveTo(baseline, x, y);
                VerifyPosition(current, x, y);
                VerifyPosition(baseline, x, y);
                Check(current.Bounds == baseline.Bounds, "final managed bounds match baseline", quiet: true);
            }
            Console.WriteLine($"200 diagonal moves: baseline={baselineMonitor.Changing}, production={currentMonitor.Changing} position operations");
            Check(baselineMonitor.Changing == 600 && currentMonitor.Changing == 200,
                "production issues one position operation per move instead of three");
            Check(!current.Visible && !IsWindowVisible(current.Handle), "hidden moves stay hidden");

            // Exercise actual layered show/hide with a fully transparent bitmap.
            // Coordinates include negative desktop space, axis-only moves, and repeats.
            using var bitmap = new Bitmap(16, 16);
            current.SetBitmap(bitmap);
            var testPoints = new (int X, int Y)[]
            {
                (-64, -64), (-64, -80), (-96, -80), (-96, -80),
                (0, 0), (100, 125), (1921, 1081), (-1920, 100)
            };
            for (var cycle = 0; cycle < 3; cycle++)
            {
                current.ShowCursorWindow();
                Check(current.Visible && IsWindowVisible(current.Handle), "show marks layered window visible");
                VerifyStyles(current);
                var size = current.Size;
                foreach (var point in testPoints)
                {
                    var before = currentMonitor.Changing;
                    current.MoveTo(point.X, point.Y);
                    VerifyPosition(current, point.X, point.Y);
                    Check(currentMonitor.Changing == before + 1, "one move for diagonal, axis-only, and repeated positions", quiet: true);
                    Check(current.Size == size && IsWindowVisible(current.Handle), "move preserves size and visibility", quiet: true);
                    VerifyStyles(current);
                    Check(LayeredWindowInterop.GetForegroundWindow() != current.Handle, "move does not leave cursor window foreground", quiet: true);
                }
                current.HideCursorWindow();
                current.MoveTo(-64, -64);
                Check(!current.Visible && !IsWindowVisible(current.Handle), "hide followed by move stays hidden");
            }
            Check(LayeredWindowInterop.GetForegroundWindow() != baseline.Handle, "baseline is not foreground");
            Console.WriteLine("PASS: native positioning, managed bounds, lazy creation, size, layered visibility, styles, and focus samples.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
    }

    // The pre-fix implementation, retained only as a regression baseline.
    private static void LegacyMoveTo(NativeCursorWindow window, int left, int top)
    {
        window.Left = left;
        window.Top = top;
        if (!window.IsHandleCreated) return;
        LayeredWindowInterop.SetWindowPos(window.Handle, LayeredWindowInterop.HWND_TOPMOST,
            left, top, 0, 0, LayeredWindowInterop.SWP_NOSIZE | LayeredWindowInterop.SWP_NOACTIVATE
            | LayeredWindowInterop.SWP_NOOWNERZORDER
            | (window.Visible ? LayeredWindowInterop.SWP_SHOWWINDOW : 0));
    }

    private static void VerifyPosition(NativeCursorWindow window, int x, int y)
    {
        Check(GetWindowRect(window.Handle, out var rect), "GetWindowRect succeeds", quiet: true);
        Check(rect.Left == x && rect.Top == y && window.Left == x && window.Top == y,
            "native and managed coordinates match requested position", quiet: true);
    }

    private static void VerifyStyles(NativeCursorWindow window)
    {
        var expected = LayeredWindowInterop.WS_EX_LAYERED | LayeredWindowInterop.WS_EX_TRANSPARENT
            | LayeredWindowInterop.WS_EX_TOOLWINDOW | LayeredWindowInterop.WS_EX_TOPMOST
            | LayeredWindowInterop.WS_EX_NOACTIVATE;
        var actual = GetWindowLongPtr(window.Handle, -20).ToInt64();
        Check((actual & expected) == expected, "layered, click-through, topmost, no-activate styles preserved", quiet: true);
    }

    private static void Check(bool condition, string name, bool quiet = false)
    {
        if (!condition) throw new InvalidOperationException(name);
        if (!quiet) Console.WriteLine("PASS: " + name);
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    private sealed class PositionMonitor : NativeWindow, IDisposable
    {
        public int Changing;
        public PositionMonitor(IntPtr hwnd) => AssignHandle(hwnd);
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x46) Changing++;
            base.WndProc(ref m);
        }
        public void Dispose() => ReleaseHandle();
    }
}
