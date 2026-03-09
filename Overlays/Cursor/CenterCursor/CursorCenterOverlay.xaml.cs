using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using PathOfWASD.Helpers;
using PathOfWASD.Internals;
using PathOfWASD.Overlays.Cursor.Interfaces;

namespace PathOfWASD.Overlays.Cursor.CenterCursor;

/// <summary>
/// Renders the midpoint-preview cursor used while the user adjusts cursor centering.
/// </summary>
public sealed class CursorCenterOverlay : IDisposable
{
    private readonly ICursorImageLoader _imageLoader;
    private NativeCursorWindow? _window;
    private Bitmap? _sourceBitmap;
    private Bitmap? _renderedBitmap;
    private double _cursorWidth = 50;
    private double _cursorHeight = 50;
    private int _left;
    private int _top;

    /// <summary>
    /// Creates the midpoint-preview renderer and loads the shared cursor image.
    /// </summary>
    public CursorCenterOverlay(ICursorImageLoader imageLoader)
    {
        _imageLoader = imageLoader;
        ReloadCursorImage();
    }

    /// <summary>
    /// Reloads the shared cursor PNG from disk and rebuilds the preview bitmap.
    /// </summary>
    public void ReloadCursorImage()
    {
        DisposeBitmap(ref _sourceBitmap);

        var cursorPath = _imageLoader.GetCursorFilePath();
        if (File.Exists(cursorPath))
        {
            using var stream = File.OpenRead(cursorPath);
            using var bitmap = new Bitmap(stream);
            _sourceBitmap = new Bitmap(bitmap);
        }

        RebuildRenderedBitmap();
    }

    /// <summary>
    /// Shows the midpoint preview and parks the real cursor at the midpoint being edited.
    /// </summary>
    public void ShowOverlay(int midX, int midY)
    {
        EnsureWindow();
        Helper.SetCursorPosDip(midX, midY);
        _window!.ShowCursorWindow();
        var windowPosition = Helper.ToPhysicalPixels(_left, _top);
        _window.MoveTo(windowPosition.X, windowPosition.Y);
    }

    /// <summary>
    /// Hides the midpoint preview window.
    /// </summary>
    public void HideOverlay()
    {
        _window?.HideCursorWindow();
    }

    /// <summary>
    /// Recomputes the preview cursor position from midpoint plus user offsets.
    /// </summary>
    public void UpdateCursorPosition(double x, double y, double midPointX, double midPointY)
    {
        _left = (int)Math.Round(midPointX - (_cursorWidth / 2) + x);
        _top = (int)Math.Round(midPointY - (_cursorHeight / 2) + y);

        if (_window != null)
        {
            var windowPosition = Helper.ToPhysicalPixels(_left, _top);
            _window.MoveTo(windowPosition.X, windowPosition.Y);
        }
    }

    /// <summary>
    /// Updates the preview cursor size and rebuilds the underlying bitmap.
    /// </summary>
    public void UpdateCursorSize(double width, double height)
    {
        _cursorWidth = Math.Max(1, width);
        _cursorHeight = Math.Max(1, height);

        RebuildRenderedBitmap();
    }

    /// <summary>
    /// Releases the preview window and cached bitmaps.
    /// </summary>
    public void Dispose()
    {
        _window?.Dispose();
        DisposeBitmap(ref _renderedBitmap);
        DisposeBitmap(ref _sourceBitmap);
    }

    /// <summary>
    /// Creates the midpoint preview window on first use.
    /// </summary>
    private void EnsureWindow()
    {
        if (_window != null)
        {
            return;
        }

        _window = new NativeCursorWindow();
        if (_renderedBitmap != null)
        {
            _window.SetBitmap(_renderedBitmap);
        }
    }

    /// <summary>
    /// Rebuilds the bitmap shown by the midpoint preview window.
    /// </summary>
    private void RebuildRenderedBitmap()
    {
        DisposeBitmap(ref _renderedBitmap);

        if (_sourceBitmap == null)
        {
            _window?.SetBitmap(null);
            return;
        }

        var physicalSize = Helper.ToPhysicalPixels(_cursorWidth, _cursorHeight);
        var pixelWidth = Math.Max(1, physicalSize.X);
        var pixelHeight = Math.Max(1, physicalSize.Y);
        var rendered = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(rendered))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(_sourceBitmap, new Rectangle(0, 0, pixelWidth, pixelHeight));
        }

        _renderedBitmap = rendered;
        _window?.SetBitmap(_renderedBitmap);
    }

    /// <summary>
    /// Disposes a cached bitmap and clears the reference.
    /// </summary>
    private static void DisposeBitmap(ref Bitmap? bitmap)
    {
        bitmap?.Dispose();
        bitmap = null;
    }
}
