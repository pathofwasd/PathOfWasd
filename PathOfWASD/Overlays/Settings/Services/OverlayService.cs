using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Gma.System.MouseKeyHook;
using Hardcodet.Wpf.TaskbarNotification;
using PathOfWASD.Helpers; 
using PathOfWASD.Managers;
using PathOfWASD.Managers.Controller;
using PathOfWASD.Managers.Cursor;
using PathOfWASD.Overlays.BGFunctionalities; 
using PathOfWASD.Overlays.Cursor;
using PathOfWASD.Overlays.Cursor.CenterCursor;
using PathOfWASD.Overlays.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Views;           
using PathOfWASD.Overlays.Settings.ViewModels;      
using Application = System.Windows.Application;

namespace PathOfWASD.Overlays.Settings.Services
{
    public class OverlayService : IDisposable
    {
        public ICommand ToggleOverlayCommand { get; }
        public ICommand ExitCommand { get; }

        private readonly SettingsOverlay _settingsOverlay;
        private readonly CursorOverlay _cursorOverlay;
        private readonly CursorCenterOverlay _cursorCenterOverlay;
        private readonly CursorManager _cursorManager;
        private readonly ControllerManager _controllerManager;
        private readonly HotkeyController _hotkeyController;
        private readonly MouseClickKeyMapper _mouseClickKeyMapper;
        private readonly IKeyboardMouseEvents _globalHook;

        private bool _isLocked;
        private bool _holdInProgress;
        private bool _centerOverlayOn;
        private readonly ICursorImageLoader _imageLoader;
        private bool _isOff = true;

        public OverlayService(
            SettingsOverlay settingsOverlay,
            CursorOverlay cursorOverlay,
            CursorCenterOverlay cursorCenterOverlay,
            IKeyboardMouseEvents globalHook,
            CursorManager cursorManager,
            ControllerManager controllerManager,
            HotkeyController hotkeyController,
            MouseClickKeyMapper mouseClickKeyMapper,
            ICursorImageLoader imageLoader)
        {
            _settingsOverlay = settingsOverlay;
            _cursorOverlay    = cursorOverlay;
            _globalHook       = globalHook;
            _cursorManager    = cursorManager;
            _controllerManager= controllerManager;
            _hotkeyController    = hotkeyController;
            _mouseClickKeyMapper = mouseClickKeyMapper;
            _cursorCenterOverlay = cursorCenterOverlay;
            _imageLoader = imageLoader;
            
            _settingsOverlay.Show();
            var vm = (SettingsViewModel)_settingsOverlay.DataContext;
            vm.OnRequestClose = _settingsOverlay.Hide;
            
            ToggleOverlayCommand = vm.ToggleOverlayCommand;
            ExitCommand = new RelayCommand(() => Exit(vm));

            vm.SaveRequested += () => OnSettingsChanged(vm);
            vm.ApplyRequested += () => OnSettingsChanged(vm);
            vm.ToggleVisualCursorRequested += async () => await ToggleVirtualCursor();
            vm.HoldToggleVisualCursorRequested += async () => await HandleHold(vm);
            vm.CenterOverlayRequested += () => CenterCursorMode(vm);
            _cursorCenterOverlay.Loaded += (_, __) =>
            {
                UpdateCursorOverlay(vm);
                UpdateCenterCursorSize(vm);
            };

            vm.CursorCenterChanged += () => UpdateCursorOverlay(vm);

            vm.VirtualCursorChanged += () =>
            {
                UploadCursor();
            };
            
            _cursorOverlay.Loaded += (_,__)=>
                UpdateCursorSize(vm);
            
            vm.CursorSizeChanged += () =>
            {
                UpdateCursorSize(vm);
                UpdateCenterCursorSize(vm);
            };

            _mouseClickKeyMapper.Start();
            _mouseClickKeyMapper.SkipLogic = true;
            _hotkeyController.RebindOnToOffWASDMode();
        }


        public void Initialize() => ShowSettingsOverlay();

        private void Exit(SettingsViewModel vm)
        {
            vm.SaveCommand.Execute(null);
            OnSettingsChanged(vm);
            (Application.Current.Resources["TrayIcon"] as TaskbarIcon)?.Dispose();
            Application.Current.Shutdown();
        }

        private async Task ToggleVirtualCursor()
        {
            if (_isLocked)
            {
                _isOff = true;
                await DeactivateVirtualCursor();
            }
            else
            {
                _isOff = false;
                await ActivateVirtualCursor();
            }
        }

        private async Task HandleHold(SettingsViewModel vm)
        {
            if (_holdInProgress) return;
            _holdInProgress = true;
            try
            {
                if (!_isOff)
                {
                    await DeactivateVirtualCursor();
                    var watcher = new KeyWatcher();
                    await watcher.WatchKeyAsync(Helper.ToVkKey(vm.HoldToggleVisualCursorKey.VirtualKey), whileHeld: () => {});
                    await ActivateVirtualCursor();   
                }
            }
            finally
            {
                _holdInProgress = false;
            }
        }
        
        private void OnSettingsChanged(SettingsViewModel vm)
        {
            UpdateManagers(vm);
            _hotkeyController.Rebind();
        }

        private void ShowSettingsOverlay()
        {
            _settingsOverlay.Show();
            UpdateManagers((SettingsViewModel)_settingsOverlay.DataContext);
        }

        private async Task ActivateVirtualCursor()
        {
            _isLocked = true;
            await _cursorManager.LockRealCursor(false, false, true);
            _mouseClickKeyMapper.SkipLogic = false;
            _hotkeyController.SkipLogic = false;
            _hotkeyController.Rebind();
        }

        private async Task DeactivateVirtualCursor()
        {
            _isLocked = false;
            await _cursorManager.JumpToVirtualCursor();
            await _controllerManager.State.DontMovePlace();
            await Task. Delay(100);
            await _controllerManager.State.DontStandInPlace();
            await _cursorManager.StopUnlockRealCursor();
            _mouseClickKeyMapper.SkipLogic = true;
            _hotkeyController.SkipLogic = true;
            _hotkeyController.RebindOnToOffWASDMode();
        }
        
        private void UpdateManagers(SettingsViewModel vm)
        {
            var toggleKeys = Helper.GetFKeyMaps(vm);
            var directionalToggleKeys = Helper.GetDirectionalFKeyMaps(vm, toggleKeys.Item2);
            
            _cursorManager.State.CursorMode = vm.CursorMode;
            _cursorManager.State.Offset      = vm.MovementOffset;
            _cursorManager.State.Sensitivity = vm.Sensitivity;
            _cursorManager.State.MidPoint    = vm.MidPoint;
            _cursorManager.State.XCursorCenterAdjustment = vm.XCursorCenter;
            _cursorManager.State.YCursorCenterAdjustment = vm.YCursorCenter;

            _controllerManager.State.MovementKey = vm.MovementKey.VirtualKey;
            _controllerManager.State.StandKey    = vm.StandKey.VirtualKey;
            _controllerManager.ToggleKeys = toggleKeys.Item1.ToList();
            _controllerManager.DirectionalKeys = directionalToggleKeys.ToList();

            _hotkeyController.UpdateToggleKeys(toggleKeys.Item1, directionalToggleKeys);
            _hotkeyController.LeftKey = vm.LeftKey.VirtualKey;
            _hotkeyController.RightKey = vm.RightKey.VirtualKey;
            _hotkeyController.MiddleKey = vm.MiddleKey.VirtualKey;

        }
        
        private void CenterCursorMode(SettingsViewModel vm)
        {
            if (_centerOverlayOn)
            {
                _cursorCenterOverlay.HideOverlay();
                _centerOverlayOn = false;
            }
            else
            {
                _cursorCenterOverlay.ShowOverlay(vm.MidPoint.X, vm.MidPoint.Y);
                _centerOverlayOn = true;
            }
        }
        
    private void UpdateCursorOverlay(SettingsViewModel vm)
    {
        _cursorCenterOverlay.UpdateCursorPosition(vm.XCursorCenter, vm.YCursorCenter, vm.MidPoint.X, vm.MidPoint.Y);
    }
    
    private void UpdateCursorSize(SettingsViewModel vm)
    {
        _cursorOverlay.UpdateCursorSize(vm.CursorSize, vm.CursorSize);
    }
    
    private async void UploadCursor()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PNG image|*.png",
            Title  = "Choose a custom cursor (PNG only)"
        };
        if (dlg.ShowDialog() != true) return;

        _imageLoader.SaveFromFile(dlg.FileName);
        
        _cursorOverlay.ReloadCursorImage();
        _cursorCenterOverlay.ReloadCursorImage();
    }
    
    private void UpdateCenterCursorSize(SettingsViewModel vm)
    {
        _cursorCenterOverlay.UpdateCursorSize(vm.CursorSize, vm.CursorSize);
    }
        public void Dispose()
        {
            _hotkeyController?.Dispose();
            _mouseClickKeyMapper?.Stop();
            _globalHook.Dispose();
        }
    }
}
