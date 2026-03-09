using System;
using System.IO;
using PathOfWASD.Overlays.Cursor.Interfaces;

namespace PathOfWASD.Overlays.Cursor;

/// <summary>
/// Persists the shared cursor PNG used by the runtime cursor renderers.
/// </summary>
public class CursorImageLoader : ICursorImageLoader
{
    private const string FileName = "cursor.png";
    private const string FolderName = "PathOfWASD";
    private static readonly string FolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);
    private static readonly string TargetPath = Path.Combine(FolderPath, FileName);

    /// <summary>
    /// Validates a PNG file and copies it into the shared LocalAppData cursor location.
    /// </summary>
    public void SaveFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", filePath);
        }

        if (!string.Equals(Path.GetExtension(filePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only .png files are allowed.");
        }

        Directory.CreateDirectory(FolderPath);
        File.Copy(filePath, TargetPath, true);
    }

    /// <summary>
    /// Returns the shared cursor PNG path consumed by the native renderers.
    /// </summary>
    public string GetCursorFilePath() => TargetPath;
}
