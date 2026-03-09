using System.Drawing;
using System.Windows.Forms;
using PathOfWASD.Internals;

namespace PathOfWASD.Overlays.Cursor;

/// <summary>
/// Hosts a tiny topmost layered window that displays the virtual cursor bitmap.
/// </summary>
internal sealed class NativeCursorWindow : Form
{
    private Bitmap? _bitmap;
    private IntPtr _previousForegroundWindow;

    /// <summary>
    /// Creates the hidden form used as the layered cursor host.
    /// </summary>
    public NativeCursorWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style = LayeredWindowInterop.WS_POPUP;
            cp.ExStyle |= LayeredWindowInterop.WS_EX_LAYERED
                          | LayeredWindowInterop.WS_EX_TRANSPARENT
                          | LayeredWindowInterop.WS_EX_TOOLWINDOW
                          | LayeredWindowInterop.WS_EX_TOPMOST
                          | LayeredWindowInterop.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>
    /// Updates the bitmap rendered by the layered window.
    /// </summary>
    public void SetBitmap(Bitmap? bitmap)
    {
        _bitmap?.Dispose();
        _bitmap = bitmap != null ? new Bitmap(bitmap) : null;

        if (IsHandleCreated && _bitmap != null)
        {
            ApplyBitmap(_bitmap);
        }
    }

    /// <summary>
    /// Shows the layered window without allowing it to steal focus.
    /// </summary>
    public void ShowCursorWindow()
    {
        if (!IsHandleCreated)
        {
            _ = Handle;
        }

        _previousForegroundWindow = LayeredWindowInterop.GetForegroundWindow();

        LayeredWindowInterop.ShowWindow(Handle, LayeredWindowInterop.SW_SHOWNOACTIVATE);
        LayeredWindowInterop.SetWindowPos(
            Handle,
            LayeredWindowInterop.HWND_TOPMOST,
            Left,
            Top,
            0,
            0,
            LayeredWindowInterop.SWP_NOSIZE
            | LayeredWindowInterop.SWP_NOACTIVATE
            | LayeredWindowInterop.SWP_NOOWNERZORDER
            | LayeredWindowInterop.SWP_SHOWWINDOW);

        RestorePreviousForegroundWindow();

        if (_bitmap != null)
        {
            ApplyBitmap(_bitmap);
        }
    }

    /// <summary>
    /// Hides the layered window while keeping the host form alive.
    /// </summary>
    public void HideCursorWindow()
    {
        if (IsHandleCreated && Visible)
        {
            LayeredWindowInterop.ShowWindow(Handle, LayeredWindowInterop.SW_HIDE);
        }
    }

    /// <summary>
    /// Moves the layered window to a new screen position.
    /// </summary>
    public void MoveTo(int left, int top)
    {
        Left = left;
        Top = top;

        if (!IsHandleCreated)
        {
            return;
        }

        LayeredWindowInterop.SetWindowPos(
            Handle,
            LayeredWindowInterop.HWND_TOPMOST,
            left,
            top,
            0,
            0,
            LayeredWindowInterop.SWP_NOSIZE
            | LayeredWindowInterop.SWP_NOACTIVATE
            | LayeredWindowInterop.SWP_NOOWNERZORDER
            | (Visible ? LayeredWindowInterop.SWP_SHOWWINDOW : 0));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == LayeredWindowInterop.WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)LayeredWindowInterop.MA_NOACTIVATE;
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>
    /// Pushes the current bitmap into the layered window with per-pixel alpha.
    /// </summary>
    private void ApplyBitmap(Bitmap bitmap)
    {
        var screenDc = LayeredWindowInterop.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return;
        }

        var memDc = LayeredWindowInterop.CreateCompatibleDC(screenDc);
        var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        var oldBitmap = LayeredWindowInterop.SelectObject(memDc, hBitmap);

        try
        {
            var topPos = new LayeredWindowInterop.POINT(Left, Top);
            var sourcePos = new LayeredWindowInterop.POINT(0, 0);
            var size = new LayeredWindowInterop.SIZE(bitmap.Width, bitmap.Height);
            var blend = new LayeredWindowInterop.BLENDFUNCTION
            {
                BlendOp = LayeredWindowInterop.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = LayeredWindowInterop.AC_SRC_ALPHA
            };

            LayeredWindowInterop.UpdateLayeredWindow(
                Handle,
                screenDc,
                ref topPos,
                ref size,
                memDc,
                ref sourcePos,
                0,
                ref blend,
                LayeredWindowInterop.ULW_ALPHA);
        }
        finally
        {
            LayeredWindowInterop.SelectObject(memDc, oldBitmap);
            LayeredWindowInterop.DeleteObject(hBitmap);
            LayeredWindowInterop.DeleteDC(memDc);
            LayeredWindowInterop.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// Restores the previous foreground window if the overlay was activated unexpectedly.
    /// </summary>
    private void RestorePreviousForegroundWindow()
    {
        if (_previousForegroundWindow == IntPtr.Zero || _previousForegroundWindow == Handle)
        {
            return;
        }

        if (LayeredWindowInterop.GetForegroundWindow() == Handle)
        {
            LayeredWindowInterop.SetForegroundWindow(_previousForegroundWindow);
        }
    }
}
