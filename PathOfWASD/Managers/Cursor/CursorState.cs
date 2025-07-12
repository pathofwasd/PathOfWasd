using PathOfWASD.Helpers;
using PathOfWASD.Helpers.Interfaces;
using PathOfWASD.Internals;
using PathOfWASD.Managers.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Models;
using WindowsInput.Native;

namespace PathOfWASD.Managers.Cursor;

public class CursorState : ICursorState
{
    public CursorState(IWpfDpiProvider dpiProvider)
    {
        (DpiScaleX, DpiScaleY) = dpiProvider.GetDpi();
        MidPoint = Helper.CalculateGameMidpoint();
    }

    public double DpiScaleX { get; set; }
    public double DpiScaleY { get; set; }
    public double RealX { get; set; }
    public double RealY { get; set; }
    public double VirtualX { get; set; }
    public double VirtualY { get; set; }
    public int XCursorCenterAdjustment { get; set; }
    public int YCursorCenterAdjustment { get; set; }
    public Win32.POINT MidPoint { get; set; }
    public double Sensitivity { get; set; } = 1.0;
    public CursorMode CursorMode { get; set; } 
    public double Offset { get; set; } = 1000;
}