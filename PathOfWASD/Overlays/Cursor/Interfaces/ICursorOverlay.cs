using PathOfWASD.Overlays.Settings.Models;

namespace PathOfWASD.Overlays.Cursor.Interfaces;

/// <summary>
/// Defines the renderer that draws the virtual cursor on screen.
/// </summary>
public interface ICursorOverlay : IDisposable
{
    /// <summary>
    /// Fires once the renderer has created its native window handle.
    /// </summary>
    event EventHandler? SourceInitialized;

    /// <summary>
    /// Reloads the cursor image from disk after the user replaces the shared PNG.
    /// </summary>
    void ReloadCursorImage();

    /// <summary>
    /// Shows the renderer for the current cursor mode.
    /// </summary>
    void ShowOverlay(CursorMode mode);

    /// <summary>
    /// Hides the renderer while preserving the existing call contract used by cursor flow code.
    /// </summary>
    void HideOverlay(CursorMode mode, bool turnOff = false, bool isClick = false);

    /// <summary>
    /// Resizes the rendered cursor sprite.
    /// </summary>
    void UpdateCursorSize(double width, double height);

    /// <summary>
    /// Moves the rendered cursor to the latest virtual-cursor position.
    /// </summary>
    void UpdateCursorPosition(double x, double y, bool notMoving, int xCursorCenterAdjustment, int yCursorCenterAdjustment);
}
