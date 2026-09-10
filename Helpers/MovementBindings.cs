using System.Windows.Input;

namespace PathOfWASD.Helpers;

public static class MovementBindings
{
    // The controller still receives the same logical W/A/S/D events.
    public static IEnumerable<(Key Physical, Key Logical)> ForLayout(bool arrows)
    {
        yield return (arrows ? Key.Up : Key.W, Key.W);
        yield return (arrows ? Key.Left : Key.A, Key.A);
        yield return (arrows ? Key.Down : Key.S, Key.S);
        yield return (arrows ? Key.Right : Key.D, Key.D);
    }

    public static bool AnyHeld(bool arrows, Func<Key, bool> isDown) =>
        ForLayout(arrows).Any(binding => isDown(binding.Physical));
}
