using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Gma.System.MouseKeyHook;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PathOfWASD.Helpers;
using PathOfWASD.Managers;
using PathOfWASD.Overlays.BGFunctionalities;
using PathOfWASD.Overlays.Cursor;
using PathOfWASD.Overlays.Settings.Services;
using PathOfWASD.Overlays.Settings.ViewModels;
using PathOfWASD.Overlays.Settings.Views;
using WindowsInput.Native;
using Application = System.Windows.Application;

namespace PathOfWASD.Startup
{
    public partial class App : Application
    {
        private IServiceProvider _services;
        private OverlayService   _overlayService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _services = AppStartup.Startup();

            _overlayService = _services.GetRequiredService<OverlayService>();
            _overlayService.Initialize();

            if (Resources["TrayIcon"] is TaskbarIcon tray)
                tray.DataContext = _overlayService;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _overlayService?.Dispose();
            base.OnExit(e);
        }
    }
}