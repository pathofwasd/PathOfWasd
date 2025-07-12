using PathOfWASD.Internals;
using PathOfWASD.Overlays.Settings.Models;
using WindowsInput.Native;

namespace PathOfWASD.Managers.Cursor.Interfaces;

public interface ICursorState
{
    double RealX { get; set; }
    double RealY { get; set; }
    double VirtualX { get; set; }
    double VirtualY { get; set; }
    int XCursorCenterAdjustment { get; set; }
    int YCursorCenterAdjustment { get; set; }
    double DpiScaleX { get; set; }
    double DpiScaleY { get; set; }
    Win32.POINT MidPoint { get; set; }
    double Sensitivity { get; set; }
    CursorMode CursorMode { get; set; }
    double Offset { get; set; }
}