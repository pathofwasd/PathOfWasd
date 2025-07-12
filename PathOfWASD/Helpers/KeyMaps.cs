using System.Windows.Input;
using WindowsInput.Native;

namespace PathOfWASD.Helpers;

public static class KeyMaps
{


        public static readonly List<Key> MasterWpfKeys;
        public static readonly List<VirtualKeyCode> AvailableVirtualKeys;

        static KeyMaps()
        {
            MasterWpfKeys = Enum.GetValues(typeof(Key))
                .Cast<Key>()
                .Where(k =>
                    (k >= Key.A && k <= Key.Z) ||
                    (k >= Key.D0 && k <= Key.D9) ||
                    (k >= Key.F1 && k <= Key.F12) ||
                    (k >= Key.NumPad0 && k <= Key.Divide) ||
                    (k >= Key.Left && k <= Key.Down) ||
                    new[]
                    {
                        Key.Space, Key.Tab, Key.Enter, Key.Escape, Key.Back,
                        Key.Insert, Key.Delete, Key.Home, Key.End, Key.PageUp, Key.PageDown,
                        Key.CapsLock, Key.NumLock, Key.Scroll,
                        Key.LeftShift, Key.RightShift,
                        Key.LeftCtrl, Key.RightCtrl,
                        Key.LeftAlt, Key.RightAlt,
                        Key.PrintScreen, Key.Pause
                    }.Contains(k))
                .ToList();
            var wasd = new[]
            {
                VirtualKeyCode.VK_W,
                VirtualKeyCode.VK_A,
                VirtualKeyCode.VK_S,
                VirtualKeyCode.VK_D
            };
            
            AvailableVirtualKeys = Enum.GetValues(typeof(VirtualKeyCode))
                .Cast<VirtualKeyCode>()
                .Where(vk =>
                    (vk >= VirtualKeyCode.VK_A && vk <= VirtualKeyCode.VK_Z) ||
                    (vk >= VirtualKeyCode.VK_0 && vk <= VirtualKeyCode.VK_9) ||
                    (vk >= VirtualKeyCode.F1 && vk <= VirtualKeyCode.F12) ||
                    (vk >= VirtualKeyCode.NUMPAD0 && vk <= VirtualKeyCode.DIVIDE) ||
                    (vk >= VirtualKeyCode.LEFT && vk <= VirtualKeyCode.DOWN) ||
                    new[]
                    {
                        VirtualKeyCode.SPACE, VirtualKeyCode.TAB, VirtualKeyCode.RETURN,
                        VirtualKeyCode.ESCAPE, VirtualKeyCode.BACK,
                        VirtualKeyCode.INSERT, VirtualKeyCode.DELETE,
                        VirtualKeyCode.HOME, VirtualKeyCode.END,
                        VirtualKeyCode.PRIOR, VirtualKeyCode.NEXT,
                        VirtualKeyCode.CAPITAL, VirtualKeyCode.NUMLOCK,
                        VirtualKeyCode.SCROLL, VirtualKeyCode.SNAPSHOT, VirtualKeyCode.PAUSE,
                        VirtualKeyCode.LSHIFT, VirtualKeyCode.RSHIFT,
                        VirtualKeyCode.LCONTROL, VirtualKeyCode.RCONTROL,
                        VirtualKeyCode.LMENU, VirtualKeyCode.RMENU
                    }.Contains(vk)
                )
                .Where(vk => !wasd.Contains(vk))
                .ToList();
        }
}