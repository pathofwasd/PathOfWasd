using WindowsInput.Native;

namespace PathOfWASD.Helpers;

// One skill can be held by the keyboard and either side button simultaneously.
internal sealed class SkillInputSources
{
    private readonly HashSet<(VirtualKeyCode Key, int Source)> _held = new();
    public VirtualKeyCode[] HeldKeys => _held.Select(input => input.Key).Distinct().ToArray();

    public bool Down(VirtualKeyCode key, int source) =>
        _held.Add((key, source)) && _held.Count(input => input.Key == key) == 1;

    public bool Up(VirtualKeyCode key, int source) =>
        _held.Remove((key, source)) && !_held.Any(input => input.Key == key);

    public void Clear() => _held.Clear();
}
