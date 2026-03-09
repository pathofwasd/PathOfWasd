using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using PathOfWASD.Internals;
using PathOfWASD.Overlays.Settings.ViewModels;
using WindowsInput.Native;

namespace PathOfWASD.Helpers;

/// <summary>
/// Collects shared input, DPI, midpoint, and key-mapping helpers used across the app.
/// </summary>
public static class Helper
{
    /// <summary>
    /// Returns whether the specified WPF key is currently down according to Win32.
    /// </summary>
    public static bool IsKeyDown(Key key)
    {
        int vk = KeyInterop.VirtualKeyFromKey(key);
        return (Win32.GetAsyncKeyState(vk) & 0x8000) != 0;
    }
    
    /// <summary>
    /// Returns whether any WASD key is currently held.
    /// </summary>
    public static bool IsWasdKeyDown()
    {
        return IsKeyDown(Key.W) ||
               IsKeyDown(Key.A) ||
               IsKeyDown(Key.S) ||
               IsKeyDown(Key.D);

    }
    
    /// <summary>
    /// Calculates the default in-game midpoint used when the user resets midpoint placement.
    /// </summary>
    public static Win32.POINT CalculateGameMidpoint()
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        double realMidpointX = screenWidth / 2;
        double realMidpointY = screenHeight / 2;

        double offsetPercent = 0.095;
        double offsetY = realMidpointY * offsetPercent;

        double gameMidpointX = realMidpointX;
        double gameMidpointY = realMidpointY - offsetY;

        return new Win32.POINT
        {
            X = (int)gameMidpointX,
            Y = (int)gameMidpointY
        };
    }

    /// <summary>
    /// Returns the DPI scale of the current WPF main window.
    /// </summary>
    public static (double ScaleX, double ScaleY) GetCurrentDpiScale()
    {
        var mainWin = System.Windows.Application.Current?.MainWindow;
        if (mainWin == null)
        {
            return (1.0, 1.0);
        }

        var dpi = VisualTreeHelper.GetDpi(mainWin);
        return (dpi.DpiScaleX, dpi.DpiScaleY);
    }

    /// <summary>
    /// Converts logical WPF coordinates into physical screen pixels for Win32 APIs.
    /// </summary>
    public static Win32.POINT ToPhysicalPixels(double x, double y)
    {
        var (scaleX, scaleY) = GetCurrentDpiScale();
        return new Win32.POINT
        {
            X = (int)Math.Round(x * scaleX),
            Y = (int)Math.Round(y * scaleY)
        };
    }

    /// <summary>
    /// Moves the real cursor using logical WPF coordinates.
    /// </summary>
    public static void SetCursorPosDip(double x, double y)
    {
        var point = ToPhysicalPixels(x, y);
        Win32.SetCursorPos(point.X, point.Y);
    }
    
    /// <summary>
    /// Converts a WinForms key value into the corresponding WPF key.
    /// </summary>
    public static Key ToWpfKey(Keys formsKey)
    {
        return KeyInterop.KeyFromVirtualKey((int)formsKey);
    }
    
    /// <summary>
    /// Converts a WPF key into the WindowsInput virtual-key type used by injection code.
    /// </summary>
    public static VirtualKeyCode ToWinFormsKey(Key wpfKey)
    {
        int virtualKey = KeyInterop.VirtualKeyFromKey(wpfKey);
        return (VirtualKeyCode)virtualKey;
    }
    
    /// <summary>
    /// Converts a WindowsInput virtual-key value into a WPF key.
    /// </summary>
    public static Key ToVkKey(VirtualKeyCode virtualKeyCode)
    {
        return KeyInterop.KeyFromVirtualKey((int)virtualKeyCode);
    }
    
    public static readonly Key[] Placeholders =
    {
        Key.F13, Key.F14, Key.F15, Key.F16,
        Key.F17, Key.F18, Key.F19, Key.F20,
        Key.F21
    };
    
    /// <summary>
    /// Builds the placeholder-key set used for non-directional skill bindings.
    /// </summary>
    public static (HashSet<Key>, int) GetFKeyMaps(SettingsViewModel vm)
    {
        var toggleKeys = new HashSet<Key>();
        int slot = 0;
        foreach (var entry in vm.ToggleEntries)
        {
            if (!entry.IsDirectional)
            {
                if (slot >= Placeholders.Length)
                    break;    

                toggleKeys.Add(Placeholders[slot++]);
            }
        
        }
        return (toggleKeys, slot);
    }
    
    /// <summary>
    /// Builds the placeholder-key set used for directional skill bindings.
    /// </summary>
    public static HashSet<Key> GetDirectionalFKeyMaps(SettingsViewModel vm, int slot)
    {
        var toggleKeys = new HashSet<Key>();
        
        foreach (var entry in vm.ToggleEntries)
        {
            if (entry.IsDirectional)
            {
                if (slot >= Placeholders.Length)
                    break;    

                toggleKeys.Add(Placeholders[slot++]);
            }
        
        }
        return toggleKeys;
    }
}
