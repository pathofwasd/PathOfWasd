namespace PathOfWASD.Internals;

public class MouseHook
{
    private static IntPtr _hookID = IntPtr.Zero;
    private static Win32.LowLevelMouseProc _proc = HookCallback;
    private static bool _isHookActive = false;

    public static void StartHook()
    {
        if (!_isHookActive)
        {
            _hookID = SetHook(_proc);
            _isHookActive = true;
        }    }

    public static void StopHook()
    {
        if (_hookID != IntPtr.Zero)
        {
            if (!Win32.UnhookWindowsHookEx(_hookID))
            {
            }
            else
            {
                _hookID = IntPtr.Zero;
                _isHookActive = false;
            }
        }
    }
    
    private static IntPtr SetHook(Win32.LowLevelMouseProc proc)
    {
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            return Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, proc,
                Win32.GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        const int WM_MOUSEMOVE = 0x0200;
        if (_isHookActive && nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
        {
            return new IntPtr(1);
        }

        return Win32.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }
}