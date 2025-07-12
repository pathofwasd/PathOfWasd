using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PathOfWASD.Internals;
using WindowsInput.Native;

namespace PathOfWASD.Overlays.BGFunctionalities
{

    public class MouseClickKeyMapper : IDisposable
    {
        private readonly Dictionary<MouseButtons, VirtualKeyCode> _mouseKeyMapping;

        private readonly HashSet<MouseButtons> _pressedButtons;
        
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private readonly LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        public bool SkipLogic { get; set; }

        public MouseClickKeyMapper(Dictionary<MouseButtons, VirtualKeyCode> mouseKeyMapping)
        {
            _mouseKeyMapping = mouseKeyMapping ?? throw new ArgumentNullException(nameof(mouseKeyMapping));
            _pressedButtons = new HashSet<MouseButtons>();
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
                _hookId = SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc,
                    Win32.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        }

        public void Stop() => Dispose();

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !SkipLogic)
            {
                var hookStruct = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                uint injectedFlags = hookStruct.flags;
                if ((injectedFlags & (Win32.LLMHF_INJECTED | Win32.LLMHF_LOWER_IL_INJECTED)) != 0)
                    return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);

                bool isDown;
                MouseButtons button;
                int wm = wParam.ToInt32();
                switch (wm)
                {
                    case Win32.WM_LBUTTONDOWN:
                        button = MouseButtons.Left;
                        isDown = true;
                        break;
                    case Win32.WM_LBUTTONUP:
                        button = MouseButtons.Left;
                        isDown = false;
                        break;
                    case Win32.WM_RBUTTONDOWN:
                        button = MouseButtons.Right;
                        isDown = true;
                        break;
                    case Win32.WM_RBUTTONUP:
                        button = MouseButtons.Right;
                        isDown = false;
                        break;
                    case Win32.WM_MBUTTONDOWN:
                        button = MouseButtons.Middle;
                        isDown = true;
                        break;
                    case Win32.WM_MBUTTONUP:
                        button = MouseButtons.Middle;
                        isDown = false;
                        break;
                    case Win32.WM_XBUTTONDOWN:
                    case Win32.WM_XBUTTONUP:
                        var hi = (hookStruct.mouseData >> 16) & 0xFFFF;
                        button = hi == 1 ? MouseButtons.XButton1 : MouseButtons.XButton2;
                        isDown = (wm == Win32.WM_XBUTTONDOWN);
                        break;
                    default:
                        return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (isDown)
                {
                    if (_mouseKeyMapping.TryGetValue(button, out var vk) && !_pressedButtons.Contains(button))
                    {
                        _pressedButtons.Add(button);
                        InjectTaggedKeyDown(vk);
                        return new IntPtr(1); 
                    }
                }
                else
                {
                    if (_mouseKeyMapping.TryGetValue(button, out var vk) && _pressedButtons.Contains(button))
                    {
                        _pressedButtons.Remove(button);
                        InjectTaggedKeyUp(vk);
                        return new IntPtr(1); 
                    }
                }
            }

            return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void InjectTaggedKeyDown(VirtualKeyCode vk) => InjectKey(vk, 0, Win32.MY_TAG);
        private void InjectTaggedKeyUp(VirtualKeyCode vk) => InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.MY_TAG);
        private void InjectLiteralKeyDown(VirtualKeyCode vk) => InjectKey(vk, 0, Win32.LITERAL_TAG);

        private void InjectLiteralKeyUp(VirtualKeyCode vk) => InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.LITERAL_TAG);

        
        private void InjectKey(VirtualKeyCode vk, uint flags, ulong tag)
        {
            var input = new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                U = new Win32.InputUnion
                {
                    ki = new Win32.KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = new UIntPtr(tag)
                    }
                }
            };
            Win32.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Win32.INPUT)));
        }
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    }
}
