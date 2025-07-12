using PathOfWASD.Internals;
using PathOfWASD.Managers.Interfaces;

namespace PathOfWASD.Managers;

public class SystemCursorManager : ISystemCursorManager
{
    private const uint SPI_SETCURSORS = 0x0057;
    private const uint SPIF_SENDCHANGE = 0x02;
    private IntPtr _hwnd;
    private IntPtr _blankCursor;
    private bool _cursorReplaced;

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public void HideSystemCursor()
    {
        if (_cursorReplaced) return;
        _blankCursor = Win32.CreateCursor(IntPtr.Zero, 0, 0, 1, 1,
            new byte[] { 0xFF }, new byte[] { 0x00 });
        Win32.SetSystemCursor(_blankCursor, Win32.OCR_NORMAL);
        _cursorReplaced = true;
    }

    public void RestoreSystemCursor()
    {
        Win32.SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE);
        _blankCursor = IntPtr.Zero;
        _cursorReplaced = false;
    }
}