using System.Windows.Input;

namespace PathOfWASD.Managers.Controller.Interfaces;

public interface IKeyStateTracker
{
    void OnDown(Key key);
    void OnUp(Key key);
    bool AnyHeld(IEnumerable<Key> keys);
    List<Key> HeldWhere(IEnumerable<Key> keys);
    bool IsHeld(Key key);
    void Clear(); 
}