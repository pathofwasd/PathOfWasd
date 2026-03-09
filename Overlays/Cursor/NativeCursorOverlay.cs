using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using PathOfWASD.Helpers;
using PathOfWASD.Helpers.Interfaces;
using PathOfWASD.Internals;
using PathOfWASD.Managers.Interfaces;
using PathOfWASD.Overlays.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Models;

namespace PathOfWASD.Overlays.Cursor;

/// <summary>
/// Renders the virtual cursor in a small native layered window.
/// </summary>
public sealed class NativeCursorOverlay : ICursorOverlay
{
    public event EventHandler? SourceInitialized;

    public double DpiScaleX { get; private set; }
    public double DpiScaleY { get; private set; }

    private readonly IWpfDpiProvider _dpiProvider;
    private readonly ICursorImageLoader _imageLoader;
    private readonly ISystemCursorManager _sysCursor;

    private NativeCursorWindow? _window;
    private Bitmap? _sourceBitmap;
    private Bitmap? _renderedBitmap;
    private double _cursorWidth = 1;
    private double _cursorHeight = 1;
    private bool _sourceInitializedRaised;

    /// <summary>
    /// Creates the native cursor renderer and loads the current shared cursor image.
    /// </summary>
    public NativeCursorOverlay(
        IWpfDpiProvider dpiProvider,
        ICursorImageLoader imageLoader,
        ISystemCursorManager sysCursor)
    {
        _dpiProvider = dpiProvider;
        _imageLoader = imageLoader;
        _sysCursor = sysCursor;

        (DpiScaleX, DpiScaleY) = _dpiProvider.GetDpi();
        ReloadCursorImage();
    }

    /// <summary>
    /// Reloads the shared cursor PNG from disk and rebuilds the cached render bitmap.
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
    /// Ensures the native window exists and makes it visible.
    /// </summary>
    public void ShowOverlay(CursorMode mode)
    {
        EnsureWindow();
        _window!.ShowCursorWindow();
    }

    /// <summary>
    /// Hides the native cursor window.
    /// </summary>
    public void HideOverlay(CursorMode mode, bool turnOff = false, bool isClick = false)
    {
        _window?.HideCursorWindow();
    }

    /// <summary>
    /// Updates the logical cursor size and rebuilds the rendered bitmap.
    /// </summary>
    public void UpdateCursorSize(double width, double height)
    {
        _cursorWidth = Math.Max(1, width);
        _cursorHeight = Math.Max(1, height);

        RebuildRenderedBitmap();
    }

    /// <summary>
    /// Moves the rendered cursor and optionally keeps the real cursor synchronized.
    /// </summary>
    public void UpdateCursorPosition(double x, double y, bool notMoving, int xCursorCenterAdjustment, int yCursorCenterAdjustment)
    {
        if (notMoving)
        {
            Win32.SetCursorPos(
                (int)Math.Round(x * DpiScaleX),
                (int)Math.Round(y * DpiScaleY)
            );
        }

        if (_window == null)
        {
            return;
        }

        var logicalLeft = x - _cursorWidth / 2 + xCursorCenterAdjustment;
        var logicalTop = y - _cursorHeight / 2 + yCursorCenterAdjustment;
        var physicalPosition = Helper.ToPhysicalPixels(logicalLeft, logicalTop);
        _window.MoveTo(physicalPosition.X, physicalPosition.Y);
    }

    /// <summary>
    /// Releases the native window and cached bitmaps.
    /// </summary>
    public void Dispose()
    {
        _window?.Dispose();
        DisposeBitmap(ref _renderedBitmap);
        DisposeBitmap(ref _sourceBitmap);
    }

    /// <summary>
    /// Creates the native window on first use and raises <see cref="SourceInitialized"/>.
    /// </summary>
    private void EnsureWindow()
    {
        if (_window != null)
        {
            return;
        }

        _window = new NativeCursorWindow();
        var hwnd = _window.Handle;
        _sysCursor.Initialize(hwnd);

        if (_renderedBitmap != null)
        {
            _window.SetBitmap(_renderedBitmap);
        }

        if (_sourceInitializedRaised)
        {
            return;
        }

        _sourceInitializedRaised = true;
        SourceInitialized?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Rebuilds the bitmap that gets pushed into the native layered window.
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
