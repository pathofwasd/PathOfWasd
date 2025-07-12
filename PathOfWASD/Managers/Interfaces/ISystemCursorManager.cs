namespace PathOfWASD.Managers.Interfaces;

public interface ISystemCursorManager
{
    void Initialize(IntPtr hwnd);
    void HideSystemCursor();
    void RestoreSystemCursor();
}