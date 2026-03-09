using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Gma.System.MouseKeyHook;
using Hardcodet.Wpf.TaskbarNotification;
using PathOfWASD.Helpers;
using PathOfWASD.Managers;
using PathOfWASD.Managers.Controller;
using PathOfWASD.Managers.Cursor;
using PathOfWASD.Overlays.BGFunctionalities;
using PathOfWASD.Overlays.Cursor.CenterCursor;
using PathOfWASD.Overlays.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Views;          
using PathOfWASD.Overlays.Settings.ViewModels;     
using Application = System.Windows.Application;

namespace PathOfWASD.Overlays.Settings.Services
{
    /// <summary>
    /// Coordinates the settings window, runtime overlays, tray actions, and hotkey rebinding.
    /// </summary>
    public class OverlayService : IDisposable
    {
        public ICommand ToggleOverlayCommand { get; }
        public ICommand ExitCommand { get; }

        private readonly SettingsOverlay _settingsOverlay;
        private readonly ICursorOverlay _cursorOverlay;
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

        /// <summary>
        /// Creates the runtime service that wires UI events into cursor and controller state.
        /// </summary>
        public OverlayService(
            SettingsOverlay settingsOverlay,
            ICursorOverlay cursorOverlay,
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

            SyncCursorRuntimePlacement(vm);
            UpdateCursorOverlay(vm);
            UpdateCenterCursorSize(vm);

            vm.CursorCenterChanged += () =>
            {
                SyncCursorRuntimePlacement(vm);
                UpdateCursorOverlay(vm);
            };

            vm.VirtualCursorChanged += () =>
            {
                UploadCursor();
            };
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

        /// <summary>
        /// Shows the settings window and synchronizes the managers with its current values.
        /// </summary>
        public void Initialize() => ShowSettingsOverlay();

        /// <summary>
        /// Saves settings and shuts down the application.
        /// </summary>
        private void Exit(SettingsViewModel vm)
        {
            vm.SaveCommand.Execute(null);
            OnSettingsChanged(vm);
            (Application.Current.Resources["TrayIcon"] as TaskbarIcon)?.Dispose();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Toggles the main virtual-cursor mode on or off.
        /// </summary>
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

        /// <summary>
        /// Temporarily disables the virtual cursor while the hold key remains pressed.
        /// </summary>
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

        /// <summary>
        /// Applies settings changes to runtime managers and rebinds hotkeys.
        /// </summary>
        private void OnSettingsChanged(SettingsViewModel vm)
        {
            UpdateManagers(vm);
            _hotkeyController.Rebind();
        }

        /// <summary>
        /// Shows the settings overlay and refreshes runtime state from its current view model.
        /// </summary>
        private void ShowSettingsOverlay()
        {
            _settingsOverlay.Show();
            UpdateManagers((SettingsViewModel)_settingsOverlay.DataContext);
        }

        /// <summary>
        /// Switches into virtual-cursor mode and reenables input remapping.
        /// </summary>
        private async Task ActivateVirtualCursor()
        {
            _isLocked = true;
            await _cursorManager.LockRealCursor(false, false, true);
            _mouseClickKeyMapper.SkipLogic = false;
            _hotkeyController.SkipLogic = false;
            _hotkeyController.Rebind();
        }

        /// <summary>
        /// Switches out of virtual-cursor mode and restores normal input handling.
        /// </summary>
        private async Task DeactivateVirtualCursor()
        {
            _isLocked = false;
            await _cursorManager.JumpToVirtualCursor();
            await _controllerManager.State.DontMovePlace();
            await Task.Delay(100);
            await _controllerManager.State.DontStandInPlace();
            await _cursorManager.StopUnlockRealCursor();
            _mouseClickKeyMapper.SkipLogic = true;
            _hotkeyController.SkipLogic = true;
            _hotkeyController.RebindOnToOffWASDMode();
        }
        
        /// <summary>
        /// Pushes the current settings values into the cursor and controller runtime state.
        /// </summary>
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
        
        /// <summary>
        /// Toggles the midpoint-preview cursor.
        /// </summary>
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

        /// <summary>
        /// Keeps the runtime cursor state aligned with midpoint edits in the settings UI.
        /// </summary>
        private void SyncCursorRuntimePlacement(SettingsViewModel vm)
        {
            _cursorManager.State.MidPoint = vm.MidPoint;
            _cursorManager.State.XCursorCenterAdjustment = vm.XCursorCenter;
            _cursorManager.State.YCursorCenterAdjustment = vm.YCursorCenter;
        }
        
        /// <summary>
        /// Updates the midpoint-preview cursor position from the current settings values.
        /// </summary>
        private void UpdateCursorOverlay(SettingsViewModel vm)
        {
            _cursorCenterOverlay.UpdateCursorPosition(vm.XCursorCenter, vm.YCursorCenter, vm.MidPoint.X, vm.MidPoint.Y);
        }
        
        /// <summary>
        /// Applies the current cursor size to the main runtime renderer.
        /// </summary>
        private void UpdateCursorSize(SettingsViewModel vm)
        {
            _cursorOverlay.UpdateCursorSize(vm.CursorSize, vm.CursorSize);
        }
        
        /// <summary>
        /// Prompts for a new PNG cursor and reloads every active cursor renderer.
        /// </summary>
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
        
        /// <summary>
        /// Applies the current cursor size to the midpoint-preview renderer.
        /// </summary>
        private void UpdateCenterCursorSize(SettingsViewModel vm)
        {
            _cursorCenterOverlay.UpdateCursorSize(vm.CursorSize, vm.CursorSize);
        }

        /// <summary>
        /// Disposes the runtime hooks and overlay windows owned by the service.
        /// </summary>
        public void Dispose()
        {
            _hotkeyController?.Dispose();
            _mouseClickKeyMapper?.Stop();
            _globalHook.Dispose();
            _cursorOverlay.Dispose();
            _cursorCenterOverlay.Dispose();
        }
    }
}
