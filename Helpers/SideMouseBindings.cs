using System.Windows.Input;
using WindowsInput.Native;

namespace PathOfWASD.Helpers;

internal static class SideMouseBindings
{
    // Separate controller slots, like NoName/Pa1/Oem102 for the original clicks.
    // Raw markers are translated to mouse output; never emitted as keyboard keys.
    public static Key Placeholder(int button) => button == 4 ? Key.CrSel : Key.EraseEof;
    public static VirtualKeyCode RawKey(int button) => (VirtualKeyCode)(-button);
    public static int Button(Key placeholder) => placeholder == Key.CrSel ? 4 : placeholder == Key.EraseEof ? 5 : 0;
}
