using System.Windows.Input;
using PathOfWASD.Managers.Controller.Interfaces;

namespace PathOfWASD.Managers.Controller;

public class KeyStateTracker : IKeyStateTracker
{
    private readonly HashSet<Key> _held = new();

    public void OnDown(Key key)  => _held.Add(key);
    public void OnUp(Key key)    => _held.Remove(key);

    public bool AnyHeld(IEnumerable<Key> keys) => keys.Any(k => _held.Contains(k));
    public List<Key> HeldWhere(IEnumerable<Key> keys) => keys.Where(k => _held.Contains(k)).ToList();

    public bool IsHeld(Key key) => _held.Contains(key);
    
    public void Clear() => _held.Clear(); 
}