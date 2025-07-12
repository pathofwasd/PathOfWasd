using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using PathOfWASD.Internals;
using PathOfWASD.Overlays.Cursor.Interfaces;

namespace PathOfWASD.Overlays.Cursor.CenterCursor;

public partial class CursorCenterOverlay : Window
{
    private readonly ICursorImageLoader _imageLoader;

    public CursorCenterOverlay(ICursorImageLoader imageLoader)
    {
        InitializeComponent();
        _imageLoader = imageLoader;
        Loaded += OnLoaded;
        
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReloadCursorImage();

        var bounds = Screen.PrimaryScreen.Bounds;
        Left   = bounds.Left;
        Top    = bounds.Top;
        Width  = bounds.Width;
        Height = bounds.Height;
    }
    
    public void ReloadCursorImage()
    {
        var img = _imageLoader.Load();
        if (img != null)
        {
            Dispatcher.Invoke(() => CursorVisual.Source = img);
        }
    }
    
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT);
    }
    public void ShowOverlay(int midX, int midY)
    {
        Win32.SetCursorPos(midX, midY);
        Show();
    }

    public void HideOverlay()
    {
        Hide();
    }
    
    public void UpdateCursorPosition(double x, double y, double midPointX, double midPointY)
    {
        Dispatcher.Invoke(() =>
        {
            Canvas.SetLeft(CursorVisual, midPointX - (CursorVisual.Width / 2) + x);
            Canvas.SetTop(CursorVisual,  midPointY - (CursorVisual.Height / 2) + y);
        });
    }
    
    public void UpdateCursorSize(double width, double height)
    {
        Dispatcher.Invoke(() =>
        {
            CursorVisual.Width  = width;
            CursorVisual.Height = height;
        });
    }
}