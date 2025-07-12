using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PathOfWASD.Internals;

public class RawMouseInputHandler
    {
        private HwndSource _hwndSource;
        private Action<int, int> _onMouseMoved;
        public bool Started { get; set; }
        
        public RawMouseInputHandler(Action<int, int> mouseMovedCallback)
        {
            _onMouseMoved = mouseMovedCallback;
        }

        public void Start()
        {
            if (Started)
            {
                return;
            }
            Started = true;
            var parameters = new HwndSourceParameters("RawInput")
            {
                WindowStyle = 0,
                ExtendedWindowStyle = 0,
                Width = 0,
                Height = 0
            };

            var helper     = new WindowInteropHelper(Application.Current.MainWindow);
            _hwndSource    = HwndSource.FromHwnd(helper.Handle)
                             ?? throw new InvalidOperationException("Couldn't get HwndSource");

            _hwndSource.AddHook(WndProc);
            RegisterRawInput(helper.Handle);
            RegisterRawInput(_hwndSource.Handle);
        }

        public void Stop()
        {
            if (!Started)
            {
                return;
            }
            Started = false;
            _hwndSource.RemoveHook(WndProc);
        }

        private void RegisterRawInput(IntPtr hwnd)
        {
            var rid = new Win32.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02; 
            rid[0].dwFlags = 0x00000100; 
            rid[0].hwndTarget = hwnd; 

            if (!Win32.RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(Win32.RAWINPUTDEVICE))))
            {
                throw new InvalidOperationException("Failed to register raw input device.");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;
            if (msg == WM_INPUT)
            {
                uint dwSize = 0;
                Win32.GetRawInputData(lParam, Win32.RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(Win32.RAWINPUTHEADER)));

                if (dwSize > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                    try
                    {
                        Win32.GetRawInputData(lParam, Win32.RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(Win32.RAWINPUTHEADER)));

                        var raw = Marshal.PtrToStructure<Win32.RAWINPUT>(buffer);
                        if (raw.header.dwType == Win32.RIM_TYPEMOUSE)
                        {
                            _onMouseMoved?.Invoke(raw.mouse.lLastX, raw.mouse.lLastY);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            return IntPtr.Zero;
        }
    }