using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using PathOfWASD.Helpers.Interfaces;
using PathOfWASD.Internals;
using PathOfWASD.Managers.Interfaces;
using PathOfWASD.Overlays.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Models;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;

namespace PathOfWASD.Overlays.Cursor
{
    public partial class CursorOverlay : Window
    {
        public double DpiScaleX { get; private set; }
        public double DpiScaleY { get; private set; }

        private readonly IWpfDpiProvider _dpiProvider;
        private readonly ICursorImageLoader _imageLoader;
        private readonly ISystemCursorManager _sysCursor;
        
        private IntPtr _hwnd;

        public CursorOverlay(
            IWpfDpiProvider dpiProvider,
            ICursorImageLoader imageLoader,
            ISystemCursorManager sysCursor)
        {
            InitializeComponent();
            _dpiProvider = dpiProvider;
            _imageLoader = imageLoader;
            _sysCursor   = sysCursor;

            (DpiScaleX, DpiScaleY) = _dpiProvider.GetDpi();

            
            Loaded += OnLoaded;
            Closing += (_, e) => _sysCursor.RestoreSystemCursor();
            Application.Current.Exit += (_, e) => _sysCursor.RestoreSystemCursor();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            _sysCursor.Initialize(_hwnd);
            
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
        

        public void SetClickThrough(bool enabled)
        {
            var exStyle = Win32.GetWindowLong(_hwnd, Win32.GWL_EXSTYLE);
            exStyle = enabled
                ? exStyle | Win32.WS_EX_TRANSPARENT
                : exStyle & ~Win32.WS_EX_TRANSPARENT;
            Win32.SetWindowLong(_hwnd, Win32.GWL_EXSTYLE, exStyle);
        }

        public void ShowOverlay(CursorMode mode)
        {
            if (mode == CursorMode.AlwaysHide) _sysCursor.HideSystemCursor();
            else if (_sysCursor != null)       _sysCursor.RestoreSystemCursor();

            Cursor = mode == CursorMode.AlwaysShow ? Cursors.Arrow : Cursors.None;
            SetClickThrough(false);
            Show();
        }

        public void HideOverlay(CursorMode mode, bool turnOff = false, bool isClick = false)
        {
            if (turnOff)
            {
                _sysCursor.RestoreSystemCursor(); Hide(); return;
            }
            switch (mode)
            {
                case CursorMode.ShowDuringSkills:
                case CursorMode.AlwaysShow:
                    _sysCursor.RestoreSystemCursor(); Hide(); break;
                case CursorMode.AlwaysHide:
                    if (!isClick) _sysCursor.HideSystemCursor();
                    else _sysCursor.RestoreSystemCursor();
                    SetClickThrough(isClick);
                    break;
                default:
                    Hide(); break;
            }
        }

        public void UpdateCursorSize(double width, double height)
        {
             Dispatcher.Invoke(() =>
            {
                CursorVisual.Width  = width;
                CursorVisual.Height = height;
            });
        }
        
        public void UpdateCursorPosition(double x, double y, bool notMoving, int xCursorCenterAdjustment, int yCursorCenterAdjustment)
        {
            if (notMoving)
                Win32.SetCursorPos(
                    (int)Math.Round(x * DpiScaleX),
                    (int)Math.Round(y * DpiScaleY)
                );

            Dispatcher.Invoke(() =>
            {
                Canvas.SetLeft(CursorVisual, x - CursorVisual.Width / 2 + xCursorCenterAdjustment);
                Canvas.SetTop(CursorVisual,  y - CursorVisual.Height / 2 + yCursorCenterAdjustment);
            });
        }
    }
}