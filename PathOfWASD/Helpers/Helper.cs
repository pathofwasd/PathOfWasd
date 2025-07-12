using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using PathOfWASD.Internals;
using PathOfWASD.Overlays.Settings.ViewModels;
using WindowsInput.Native;

namespace PathOfWASD.Helpers;

public static class Helper
{
    public static bool IsKeyDown(Key key)
    {
        int vk = KeyInterop.VirtualKeyFromKey(key);
        return (Win32.GetAsyncKeyState(vk) & 0x8000) != 0;
    }
    
    public static bool IsWasdKeyDown()
    {
        return IsKeyDown(Key.W) ||
               IsKeyDown(Key.A) ||
               IsKeyDown(Key.S) ||
               IsKeyDown(Key.D);

    }
    
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
    
    public static Key ToWpfKey(Keys formsKey)
    {
        return KeyInterop.KeyFromVirtualKey((int)formsKey);
    }
    

    public static VirtualKeyCode ToWinFormsKey(Key wpfKey)
    {
        int virtualKey = KeyInterop.VirtualKeyFromKey(wpfKey);
        return (VirtualKeyCode)virtualKey;
    }
    
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
