using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using PathOfWASD.Overlays.Cursor.CenterCursor;
using PathOfWASD.Overlays.Settings.ViewModels;
using Application = System.Windows.Application;

namespace PathOfWASD.Overlays.Settings.Views
{
    public partial class SettingsOverlay
    {
        private bool _isCustomMaximized = false;
        private Rect _restoreBounds;
        public ICommand ExitCommand { get; }

        private readonly SettingsViewModel vm;

        private readonly ISettingService _settingsService;

        public SettingsOverlay(SettingsViewModel viewModel, ISettingService settingsService)
        {
            InitializeComponent();

            vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _settingsService = settingsService;

            var iconUri = new Uri(
                "pack://application:,,,/PathOfWASD;component/Asset/moveexile.ico",
                UriKind.Absolute
            );
            Icon = BitmapFrame.Create(iconUri);

            vm.OnRequestClose += Hide;
            vm.ToggleOverlayRequested += ToggleOverlay;

            ExitCommand = new RelayCommand(OnExit);

            DataContext = vm;

        }

        private void OnExit()
        {
            if (Application.Current.Resources["TrayIcon"] is TaskbarIcon tray)
                tray.Dispose();

            Application.Current.Shutdown();
        }

        private void ToggleOverlay()
        {
            if (IsVisible)
                Hide();
            else
            {
                Show();
                Activate();
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
        
        private void SettingsOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.Load();
            
            if (vm.AppWidthSize > 0 && vm.AppHeightSize > 0)
            {
                this.Width  = settings.AppWidthSize;
                this.Height = settings.AppHeightSize;
            }
        }
        
        private void SettingsOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var newW = e.NewSize.Width;
            var newH = e.NewSize.Height;

            vm.UpdateAppSize((int)newW, (int)newH);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            vm.SaveCommand.Execute(null);

            if (Application.Current.Resources["TrayIcon"] is TaskbarIcon tray)
                tray.Dispose();

            Application.Current.Shutdown();
        }
        
        private void MaxMinButton_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow((DependencyObject)sender);
            if (window == null) return;

            var hwnd = new WindowInteropHelper(window).Handle;
            var screen = Screen.FromHandle(hwnd); 
            var workingArea = screen.WorkingArea;

            if (_isCustomMaximized)
            {
                window.Left = _restoreBounds.Left;
                window.Top = _restoreBounds.Top;
                window.Width = _restoreBounds.Width;
                window.Height = _restoreBounds.Height;

                _isCustomMaximized = false;
            }
            else
            {
                _restoreBounds = new Rect(window.Left, window.Top, window.Width, window.Height);

                double customWidth = 590;
                window.Top = workingArea.Top;
                window.Height = workingArea.Height;

                window.Left = workingArea.Left + (workingArea.Width - customWidth) / 2;
                window.Width = customWidth;
                _isCustomMaximized = true;
            }
            
            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
                window.Width = 590;
                window.Height = 800;
                _isCustomMaximized = false;
            }
        }
    }
}
