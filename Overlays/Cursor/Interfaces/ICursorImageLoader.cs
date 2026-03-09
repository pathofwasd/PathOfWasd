namespace PathOfWASD.Overlays.Cursor.Interfaces;

/// <summary>
/// Persists and locates the shared cursor image used by runtime renderers.
/// </summary>
public interface ICursorImageLoader
{
    /// <summary>
    /// Validates a user-selected PNG and copies it into the app's cursor storage location.
    /// </summary>
    void SaveFromFile(string filePath);

    /// <summary>
    /// Returns the on-disk cursor path that native renderers load at runtime.
    /// </summary>
    string GetCursorFilePath();
}
