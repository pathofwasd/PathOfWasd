
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PathOfWASD.Internals;
using PathOfWASD.Managers;
using PathOfWASD.Overlays.Cursor;
using PathOfWASD.Overlays.Settings.Services;
using PathOfWASD.Overlays.Settings.ViewModels;
using PathOfWASD.Overlays.Settings.Views;
using WindowsInput.Native;
using Gma.System.MouseKeyHook;
using PathOfWASD.Helpers;
using PathOfWASD.Helpers.Interfaces;
using PathOfWASD.Managers.Controller;
using PathOfWASD.Managers.Controller.Interfaces;
using PathOfWASD.Managers.Cursor;
using PathOfWASD.Managers.Cursor.Interfaces;
using PathOfWASD.Managers.Interfaces;
using PathOfWASD.Overlays.BGFunctionalities;
using PathOfWASD.Overlays.Cursor.CenterCursor;
using PathOfWASD.Overlays.Cursor.Interfaces;
using Application = System.Windows.Application;

namespace PathOfWASD.Startup
{
    public static class AppStartup
    {
        private const string MutexName = "Global\\PathOfWASD_Mutex";
        private static Mutex? _singleInstanceMutex;
        
        private static readonly IntPtr DPI_AWARE_CTX = new IntPtr(-4);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
        
        public static void EnsureSingleInstance()
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name:          MutexName,
                createdNew:    out createdNew
            );

            if (!createdNew)
            {
                var me = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(me.ProcessName))
                {
                    if (p.Id != me.Id)
                    {
                        try { p.Kill(); p.WaitForExit(); }
                        catch {  }
                    }
                }

                _singleInstanceMutex = new Mutex(
                    initiallyOwned: true,
                    name:          MutexName,
                    createdNew:    out createdNew
                );
            }
        }
        
        public static void ConfigureDpi() => SetProcessDpiAwarenessContext(DPI_AWARE_CTX);
        
        public static void EnsureCursorFile()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder  = Path.Combine(appData, "PathOfWASD");
            var cursorFile = Path.Combine(folder, "cursor.png");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            if (!File.Exists(cursorFile))
            {
                var uri = new Uri("pack://application:,,,/PathOfWASD;component/Asset/cursor4.png");
                var info = Application.GetResourceStream(uri);
                if (info?.Stream != null)
                {
                    using var input = info.Stream;
                    using var output = File.Create(cursorFile);
                    input.CopyTo(output);
                }
            }
        }


        public static IServiceProvider ConfigureServices()
        {
            var sc = new ServiceCollection();

            sc.AddSingleton<ISettingService, Settings>();
            sc.AddSingleton<SettingsViewModel>();
            sc.AddSingleton<ICursorImageLoader, CursorImageLoader>();
            sc.AddSingleton<SettingsOverlay>(sp => new SettingsOverlay(sp.GetRequiredService<SettingsViewModel>(), sp.GetRequiredService<ISettingService>()));

            sc.AddSingleton<IWpfDpiProvider, WpfDpiProvider>();
            sc.AddSingleton<ISystemCursorManager, SystemCursorManager>();

            sc.AddSingleton<CursorOverlay>();

            sc.AddSingleton<IKeyboardMouseEvents>(_ => Hook.GlobalEvents());
            sc.AddSingleton<IKeyStateTracker, KeyStateTracker>();
            sc.AddSingleton<DelayMovementUpState>();
            sc.AddSingleton<ICursorState, CursorState>();
            sc.AddSingleton<CursorManager>();

            sc.AddSingleton<IControllerState, ControllerState>();

            sc.AddSingleton<ISkillUpDelayHandler, SkillUpDelayHandler>();
            sc.AddSingleton<IEventProcessor, EventProcessor>();

            sc.AddSingleton<ControllerManager>();

            sc.AddSingleton(provider =>
                new Lazy<ControllerManager>(() => provider.GetRequiredService<ControllerManager>()));
            sc.AddSingleton(provider =>
                new Lazy<IEventProcessor>(() => provider.GetRequiredService<IEventProcessor>()));
            sc.AddSingleton<HotkeyController>(sp => new HotkeyController(
                sp.GetRequiredService<SettingsOverlay>(),
                sp.GetRequiredService<SettingsViewModel>(),
                sp.GetRequiredService<ControllerManager>()));
            sc.AddSingleton<MouseClickKeyMapper>(sp => new MouseClickKeyMapper(new Dictionary<MouseButtons, VirtualKeyCode>
            {
                [MouseButtons.Left]  = Helper.ToWinFormsKey(Key.NoName),
                [MouseButtons.Right] = Helper.ToWinFormsKey(Key.Pa1),
                [MouseButtons.Middle] = Helper.ToWinFormsKey(Key.Oem102)
            }));

            sc.AddSingleton<CursorCenterOverlay>();

            sc.AddSingleton<OverlayService>();

            return sc.BuildServiceProvider();
        }
        
        public static IServiceProvider Startup()
        {
            EnsureSingleInstance();
            ConfigureDpi();
            EnsureCursorFile();
            return ConfigureServices();
        }
    }
}
