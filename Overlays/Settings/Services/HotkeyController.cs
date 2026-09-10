using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PathOfWASD.Helpers;
using PathOfWASD.Internals;
using PathOfWASD.Managers;
using PathOfWASD.Managers.Controller;
using PathOfWASD.Overlays.Settings.Models;
using PathOfWASD.Overlays.Settings.ViewModels;
using WindowsInput;
using WindowsInput.Native;


namespace PathOfWASD.Overlays.Settings.Services
{
    /// <summary>
    /// Owns global hotkey registration, low-level keyboard interception, and skill remapping.
    /// </summary>
    public class HotkeyController : IDisposable
    {
        private readonly HashSet<VirtualKeyCode> _physicalPressed = new();
        private sealed record SideButtonPress(int Button, VirtualKeyCode Output, VirtualKeyCode Placeholder, bool AsMouse, int Generation, int SourceId);
        private readonly Dictionary<int, SideButtonPress> _heldSideButtons = new();
        private readonly SkillInputSources _outputSources = new();
        private int _bindingGeneration;
        private int _nextSideSource;
        private readonly Action<VirtualKeyCode, uint, ulong> _sendKey;
        private readonly Action<int, bool> _sendSideMouse;
        private readonly Func<Key, bool> _isKeyDown;
        public VirtualKeyCode? Mouse4Key { get; set; }
        public VirtualKeyCode? Mouse5Key { get; set; }
        public bool SideButtonsEnabled { get; set; }
        private readonly ControllerManager _controllerManager;
        
        private HwndSource _hwndSource;
        private readonly SettingsViewModel _viewModel;
        private readonly Dictionary<int, Action> _callbacks = new();
        private readonly Dictionary<VirtualKeyCode, Action> _releaseCallbacks = new();

        private readonly Dictionary<int, VirtualKeyCode> _idToKey = new();
        private readonly Dictionary<VirtualKeyCode, Action> _literalDownCallbacks = new();
        private readonly Dictionary<VirtualKeyCode, Action> _literalUpCallbacks   = new();
        private readonly HashSet<VirtualKeyCode> _pressedKeys = new();
        public bool SkipLogic { get; set; } = false;
        private Win32.LowLevelKeyboardProc _keyboardProc;
        private IntPtr _keyboardHookId;

        private List<Key> _suspendedSkillsDuringDirectionalInput = new();
        private readonly Dictionary<VirtualKeyCode, Action> _skillDownCallbacks = new();

        private readonly Dictionary<VirtualKeyCode, VirtualKeyCode> _swapMap
            = new Dictionary<VirtualKeyCode, VirtualKeyCode>();
        private readonly Dictionary<VirtualKeyCode, TaskCompletionSource<bool>> _releaseTcs
            = new Dictionary<VirtualKeyCode, TaskCompletionSource<bool>>();
        private List<VirtualKeyCode> _pendingDirectionalRestoreKeys = new();
        private void InjectPlaceholderKeyDown(VirtualKeyCode vk) => InjectKey(vk, 0, Win32.PlaceholderInjectionTag);
        private void InjectPlaceholderKeyUp(VirtualKeyCode vk) => InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.PlaceholderInjectionTag);
        private void InjectDirectionalRestoreKeyUp(VirtualKeyCode vk)
        {
            if (!_pendingDirectionalRestoreKeys.Contains(vk))
            {
                _pendingDirectionalRestoreKeys.Add(vk);
                InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.DirectionalRestoreInjectionTag);
            }
        }

        private void InjectLiteralKeyDown(VirtualKeyCode vk) => InjectKey(vk, 0, Win32.LiteralInjectionTag);
        private async Task InjectDirectionalRestoreKeyDown(VirtualKeyCode vk)
        {
            if (_pendingDirectionalRestoreKeys.Contains(vk))
            {
                await Task.Delay(40);
                _pendingDirectionalRestoreKeys.Remove(vk);
                InjectKey(vk, 0, Win32.DirectionalRestoreInjectionTag);
            }
        }

        private void InjectLiteralKeyUp(VirtualKeyCode vk) => InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.LiteralInjectionTag);

        private void InjectKey(VirtualKeyCode vk, uint flags, ulong tag)
        {
            if (vk == SideMouseBindings.RawKey(4)) _sendSideMouse(4, flags == 0);
            else if (vk == SideMouseBindings.RawKey(5)) _sendSideMouse(5, flags == 0);
            else _sendKey(vk, flags, tag);
        }

        private static void SendSideMouse(int button, bool down)
        {
            var input = new Win32.INPUT
            {
                type = (int)Win32.INPUT_MOUSE,
                U = new Win32.InputUnion { mi = new Win32.MOUSEINPUT
                {
                    mouseData = (uint)(button - 3),
                    dwFlags = down ? 0x0080u : 0x0100u, // MOUSEEVENTF_XDOWN / XUP
                    dwExtraInfo = new UIntPtr(Win32.LiteralInjectionTag)
                } }
            };
            Win32.SendInput(1, new[] { input }, Marshal.SizeOf<Win32.INPUT>());
        }

        private static void SendKey(VirtualKeyCode vk, uint flags, ulong tag)
        {
            var input = new Win32.INPUT {
                type = Win32.INPUT_KEYBOARD,
                U = new Win32.InputUnion {
                    ki = new Win32.KEYBDINPUT {
                        wVk         = (ushort)vk,
                        wScan       = 0,
                        dwFlags     = flags,
                        time        = 0,
                        dwExtraInfo = new UIntPtr(tag)
                    }
                }
            };
            Win32.SendInput(1, new[] { input }, Marshal.SizeOf<Win32.INPUT>());
        }

        public HashSet<Key> ToggleKeys { get; private set; } = new HashSet<Key>();
        public VirtualKeyCode LeftKey { get; set; }
        public VirtualKeyCode RightKey { get; set; }
        public VirtualKeyCode MiddleKey { get; set; }
        public void UpdateToggleKeys(IEnumerable<Key> newKeys, IEnumerable<Key> directionalNewKeys)
        {
            ToggleKeys = new HashSet<Key>(newKeys);
            _controllerManager.ToggleKeys = new List<Key>(newKeys);
            _controllerManager.DirectionalKeys = new List<Key>(directionalNewKeys);
           RebindOnToOffWASDMode();
        }
        
        private readonly InputSimulator _sim = new();
        private bool _toggledOff;

        /// <summary>
        /// Creates the hotkey controller and delays hook setup until the settings window has an HWND.
        /// </summary>
        public HotkeyController(Window window, SettingsViewModel viewModel, ControllerManager controllerManager,
            Action<VirtualKeyCode, uint, ulong>? sendKey = null,
            Action<int, bool>? sendSideMouse = null, Func<Key, bool>? isKeyDown = null)
        {
            _sendKey = sendKey ?? SendKey;
            _sendSideMouse = sendSideMouse ?? SendSideMouse;
            _isKeyDown = isKeyDown ?? Helper.IsKeyDown;
            _sim = new InputSimulator();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));

            if (window == null) throw new ArgumentNullException(nameof(window));
            window.SourceInitialized += OnSourceInitialized; 
        }
        
        /// <summary>
        /// Finishes hook setup once the settings window has a native handle.
        /// </summary>
        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var window = (Window)sender;
            var helper = new WindowInteropHelper(window);
            _hwndSource = HwndSource.FromHwnd(helper.Handle)
                          ?? throw new InvalidOperationException("Couldn't get HwndSource");
            _hwndSource.AddHook(WndProc);

            _keyboardProc = HookCallback;
            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                var moduleHandle = Win32.GetModuleHandle(module.ModuleName);
                _keyboardHookId = Win32.SetWindowsHookEx(
                    Win32.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            }

            BindAll();

            window.SourceInitialized -= OnSourceInitialized; 
        }

        /// <summary>
        /// Rebuilds all hotkey registrations using the current settings values.
        /// </summary>
        public void Rebind()
        {
            ReleaseActiveInputs();
            foreach (var id in _callbacks.Keys.ToList())
                Win32.UnregisterHotKey(_hwndSource.Handle, id);
            _callbacks.Clear();
            _idToKey.Clear();

            _releaseCallbacks.Clear();

            _skillDownCallbacks.Clear();

            _pressedKeys.Clear();

            BindAll();
        }
        
        /// <summary>
        /// Rebuilds the hotkeys for the "virtual cursor disabled" state.
        /// </summary>
        public void RebindOnToOffWASDMode()
        {
            ReleaseActiveInputs();
            foreach (var id in _callbacks.Keys.ToList())
                Win32.UnregisterHotKey(_hwndSource.Handle, id);
            _callbacks.Clear();
            _idToKey.Clear();

            _releaseCallbacks.Clear();

            _skillDownCallbacks.Clear();

            _pressedKeys.Clear();

            BindAll(true);
        }

        /// <summary>
        /// Registers the current swap map and resets any synthetic key state left from prior binds.
        /// </summary>
        private void BindAll(bool skip = false)
        {
            if (!skip)
            {
                _toggledOff = false;
                BindToggleEntries();
            }
            else
            {
                _toggledOff = true;
                _swapMap.Clear();
                _skillDownCallbacks  .Clear();
                _releaseCallbacks    .Clear();
                _literalDownCallbacks.Clear();
                _literalUpCallbacks  .Clear();
            }
            
            int slot = 0;
            foreach (var entry in _viewModel.ToggleEntries)
            {
                if (slot >= Helper.Placeholders.Length)
                    break;

                var placeholderKey = Helper.Placeholders[slot++];
               
                var selectedEntryKey = entry.SelectedKey;
                _sim.Keyboard.KeyUp(selectedEntryKey.VirtualKey);
                _sim.Keyboard.KeyUp(Helper.ToWinFormsKey( placeholderKey));
            }
            _sim.Keyboard.KeyUp(Helper.ToWinFormsKey(Key.F23));
            _sim.Keyboard.KeyUp(Helper.ToWinFormsKey( Key.NoName));
            _sim.Keyboard.KeyUp(Helper.ToWinFormsKey(Key.F24));
            _sim.Keyboard.KeyUp(Helper.ToWinFormsKey( Key.Pa1));
            _sim.Keyboard.KeyUp(Helper.ToWinFormsKey(Key.F22));
            _sim.Keyboard.KeyUp(Helper.ToWinFormsKey( Key.Oem102));
            _sim.Keyboard.KeyUp(_viewModel.LeftKey.VirtualKey);
            _sim.Keyboard.KeyUp(_viewModel.RightKey.VirtualKey);
            _sim.Keyboard.KeyUp(_viewModel.MiddleKey.VirtualKey);
            _sim.Keyboard.KeyUp(_viewModel.MovementKey.VirtualKey);
            _sim.Keyboard.KeyUp(_viewModel.StandKey.VirtualKey);

            _controllerManager.ClearStates();
        }

        private void BindMouseKeys()
        {
            RegisterCallbacks(Key.F23, Key.NoName);
            RegisterCallbacks(Key.F24, Key.Pa1);
            RegisterCallbacks(Key.F22, Key.Oem102);
            foreach (var button in new[] { 4, 5 })
            {
                if ((button == 4 ? Mouse4Key : Mouse5Key) == null) continue;
                var placeholder = Helper.ToWinFormsKey(SideMouseBindings.Placeholder(button));
                _swapMap[SideMouseBindings.RawKey(button)] = placeholder;
                _skillDownCallbacks[placeholder] = () =>
                {
                    if (_heldSideButtons.TryGetValue(button, out var press))
                        _ = HandleMappedKeyDown(press.Output, placeholder, true, press);
                };
            }
        }
        
        private bool AltMode => _viewModel.InvertAltMode ? _isKeyDown(Helper.ToVkKey(_viewModel.HoldToggleAltKey.VirtualKey)) : !_isKeyDown(Helper.ToVkKey(_viewModel.HoldToggleAltKey.VirtualKey));

        public bool HandleSideButton(int button, bool isDown)
        {
            if (button is not (4 or 5)) return false;
            if (isDown)
            {
                if (_heldSideButtons.ContainsKey(button)) return true;
                var key = button == 4 ? Mouse4Key : Mouse5Key;
                var placeholder = Helper.ToWinFormsKey(SideMouseBindings.Placeholder(button));
                if (!SideButtonsEnabled || SkipLogic || _toggledOff || key == null
                    || !_skillDownCallbacks.TryGetValue(placeholder, out var down)) return false;
                bool asMouse = AltMode;
                _heldSideButtons.Add(button, new SideButtonPress(button,
                    asMouse ? SideMouseBindings.RawKey(button) : key.Value, placeholder, asMouse, _bindingGeneration, --_nextSideSource));
                down();
                return true;
            }

            // Consume the matching release even after mode-off or a binding change.
            if (!_heldSideButtons.Remove(button, out var held)) return false;
            if (held.Generation == _bindingGeneration && _releaseCallbacks.TryGetValue(held.Placeholder, out var up)) up();
            return true;
        }

        public void ReleaseActiveInputs()
        {
            _bindingGeneration++;
            foreach (var press in _heldSideButtons.Values)
            {
                if (_releaseCallbacks.TryGetValue(press.Placeholder, out var release)) release();
                _pendingDirectionalRestoreKeys.Remove(press.Output);
            }
            foreach (var key in _physicalPressed.Concat(_outputSources.HeldKeys).Distinct().ToArray())
            {
                if (_swapMap.TryGetValue(key, out var placeholder)
                    && _releaseCallbacks.TryGetValue(placeholder, out var up)) up();
                InjectLiteralKeyUp(key);
            }
            _physicalPressed.Clear();
            _outputSources.Clear();
        }
        
        /// <summary>
        /// Builds the placeholder-key map for skill bindings and the fixed WASD handlers.
        /// </summary>
        private void BindToggleEntries()
        {
            _swapMap.Clear();
            _skillDownCallbacks  .Clear();
            _releaseCallbacks    .Clear();
            _literalDownCallbacks.Clear();
            _literalUpCallbacks  .Clear();

            int slot = 0;
            foreach (var entry in _viewModel.ToggleEntries)
            {
               if (slot >= Helper.Placeholders.Length)
                        break;

               var placeholderKey = Helper.Placeholders[slot++];
               
               var selectedEntryKey = entry.SelectedKey;
                RegisterCallbacks(Helper.ToVkKey(selectedEntryKey.VirtualKey), placeholderKey);
            }

            BindMouseKeys();
            
            foreach (var binding in MovementBindings.ForLayout(_controllerManager.UseArrowKeys))
            {
                _skillDownCallbacks[Helper.ToWinFormsKey(binding.Physical)] = () =>
                {
                    _ = HandleWASDKeyDown(Helper.ToWinFormsKey(binding.Logical));
                };
                _releaseCallbacks[Helper.ToWinFormsKey(binding.Physical)] = () =>
                {
                    _ = HandleWASDKeyUp(Helper.ToWinFormsKey(binding.Logical));
                };
            }
        }

        /// <summary>
        /// Registers the callback chain for one physical key and its placeholder binding.
        /// </summary>
        private void RegisterCallbacks(Key selectedEntryKey, Key placeholderKey)
        {
            var physicalVk    = (VirtualKeyCode)KeyInterop.VirtualKeyFromKey(selectedEntryKey);
            var placeholderVk = Helper.ToWinFormsKey(placeholderKey);
            _swapMap[physicalVk] = placeholderVk;
            
            _skillDownCallbacks[placeholderVk] = () =>
            {
                var isClick = false;
                if (!AltMode)
                {
                    if (placeholderKey == Key.NoName)
                    {
                        _ = HandleMappedKeyDown(LeftKey, placeholderVk, true);
                    }
                    else if (placeholderKey == Key.Pa1)
                    {
                        _ = HandleMappedKeyDown(RightKey, placeholderVk, true);
                    }
                    else if (placeholderKey == Key.Oem102)
                    {
                        _ = HandleMappedKeyDown(MiddleKey, placeholderVk, true);
                    }
                    else
                    {
                        _ = HandleMappedKeyDown(physicalVk, placeholderVk);
                    }
                }
                else
                {
                    if (placeholderKey == Key.NoName || placeholderKey == Key.Pa1 || placeholderKey == Key.Oem102)
                    {
                        isClick = true;
                    }
                    _ = HandleMappedKeyDown(physicalVk, placeholderVk, isClick);
                }

            };
            _releaseCallbacks[placeholderVk] = () =>
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _releaseTcs[placeholderVk] = tcs;

                _releaseCallbacks[placeholderVk] = () =>
                {
                    if (_releaseTcs.TryGetValue(placeholderVk, out var src))
                    {
                        src.TrySetResult(true);
                        _releaseTcs.Remove(placeholderVk);
                    }
                };
                
            };

            _literalDownCallbacks[physicalVk] = () =>
            {
                  if (physicalVk == VirtualKeyCode.F23 && AltMode)
                  {
                      _sim.Mouse.LeftButtonDown();
                  }
                  if (physicalVk == VirtualKeyCode.F24 && AltMode)  
                  {
                      _sim.Mouse.RightButtonDown();
                  }   
                  if (physicalVk == VirtualKeyCode.F22 && AltMode)  
                  {
                      _sim.Mouse.MiddleButtonDown();
                  }   
            };
            _literalUpCallbacks  [physicalVk] = () =>
            {
                if (physicalVk == VirtualKeyCode.F23 || physicalVk == LeftKey)
                {
                    _sim.Mouse.LeftButtonUp();
                }
                if (physicalVk == VirtualKeyCode.F24 || physicalVk == RightKey)
                {
                    _sim.Mouse.RightButtonUp();
                }
                if (physicalVk == VirtualKeyCode.F22 || physicalVk == MiddleKey)
                {
                    _sim.Mouse.MiddleButtonUp();
                }
            };
        }


        /// <summary>
        /// Runs the ordered mapped key-down pipeline: controller work, literal injection, then release wait.
        /// </summary>
        private async Task HandleMappedKeyDown(VirtualKeyCode physicalVk, VirtualKeyCode? placeholderVk = null, bool isClick = false, SideButtonPress? sidePress = null)
{
    int? generation = isClick && sidePress == null ? null : _bindingGeneration;
    if (!placeholderVk.HasValue)
    {
        placeholderVk = _swapMap[physicalVk];
    }
    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    _releaseCallbacks[placeholderVk.Value] = () =>
    {
        tcs.TrySetResult(true);
        _releaseCallbacks.Remove(placeholderVk.Value);
    };
    if (SkipLogic)
    {
        InjectLiteralKeyDown(physicalVk);
        await HandleMappedKeyUp(physicalVk, placeholderVk.Value, isClick, generation, sidePress);
        return;
    }
    await Task.Delay(8);
    if (generation.HasValue && generation != _bindingGeneration) return;
    var isDirectionalSkill = _controllerManager.DirectionalKeys.Contains(Helper.ToVkKey(placeholderVk.Value));
    
    if (_controllerManager.State.AnyOtherWASDIsCurrentlyHeldDown && isDirectionalSkill)
    {
        _suspendedSkillsDuringDirectionalInput = _controllerManager.State.AllHeldSkillDown(_controllerManager.ToggleKeys);

        foreach (var skillKey in _suspendedSkillsDuringDirectionalInput.ToList())
        {
            var sideButton = SideMouseBindings.Button(skillKey);
            if (sideButton != 0)
            {
                if (_heldSideButtons.TryGetValue(sideButton, out var heldSide)) InjectDirectionalRestoreKeyUp(heldSide.Output);
                else _suspendedSkillsDuringDirectionalInput.Remove(skillKey);
                continue;
            }
            var mappedKey = _swapMap.FirstOrDefault(kvp => kvp.Value == Helper.ToWinFormsKey( skillKey)).Key;
            if (mappedKey == VirtualKeyCode.F23)
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(LeftKey)))
                {
                    _suspendedSkillsDuringDirectionalInput.Remove(skillKey);
                }
                else
                {
                    InjectDirectionalRestoreKeyUp(LeftKey);
                }
                
            }
            else if (mappedKey == VirtualKeyCode.F24)
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(RightKey)))
                {
                    _suspendedSkillsDuringDirectionalInput.Remove(skillKey);
                }
                else
                {
                    InjectDirectionalRestoreKeyUp(RightKey);
                }
            }
            else if (mappedKey == VirtualKeyCode.F22)
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(MiddleKey)))
                {
                    _suspendedSkillsDuringDirectionalInput.Remove(skillKey);
                }
                else
                {
                    InjectDirectionalRestoreKeyUp(MiddleKey);
                }
            }
            else
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(mappedKey)))
                {
                    _suspendedSkillsDuringDirectionalInput.Remove(skillKey);
                }
                else
                {
                    InjectDirectionalRestoreKeyUp(mappedKey);
                }
            }

        }
    }
    var op = Application.Current.Dispatcher.InvokeAsync(
        () => _controllerManager.HandleKeyDownAsync(
            KeyInterop.KeyFromVirtualKey((int)placeholderVk), sidePress?.AsMouse ?? (isClick && AltMode))
    );
    var inner = await op; 
    await inner;          

    await Task.Delay(24);
    if (generation.HasValue && generation != _bindingGeneration) return;

    if (!generation.HasValue || _outputSources.Down(physicalVk, sidePress?.SourceId ?? (int)placeholderVk.Value))
        InjectLiteralKeyDown(physicalVk);



    await tcs.Task;
    
    await Task.Delay(8);
    if (generation.HasValue && generation != _bindingGeneration) return;

    await HandleMappedKeyUp(physicalVk, placeholderVk.Value, isClick, generation, sidePress);
}


        
        /// <summary>
        /// Runs the mapped key-up pipeline and restores directional state after placeholder release.
        /// </summary>
        private async Task HandleMappedKeyUp(VirtualKeyCode physicalVk, VirtualKeyCode placeholderVk, bool isClick, int? generation, SideButtonPress? sidePress)
        {
            if (SkipLogic)
            {
                InjectLiteralKeyUp(physicalVk);
                return;
            }

            if (physicalVk == VirtualKeyCode.F23 && AltMode)
            {
                _sim.Mouse.LeftButtonUp();
                await Task.Delay(20);
            }

            var isDirectionalUp = _controllerManager.DirectionalKeys.Contains(Helper.ToVkKey( placeholderVk));
            var op = Application.Current.Dispatcher.InvokeAsync(
                () => _controllerManager.HandleKeyUpAsync(
                    KeyInterop.KeyFromVirtualKey((int)placeholderVk), sidePress?.AsMouse ?? (isClick && AltMode))
            );

            var inner = await op;     

            await inner;

            if (generation.HasValue && generation != _bindingGeneration) return;

            if (!isDirectionalUp)
            {
                if (_suspendedSkillsDuringDirectionalInput.Any())
                {
                    if (_suspendedSkillsDuringDirectionalInput.Contains(Helper.ToVkKey(placeholderVk)))
                    {
                        _suspendedSkillsDuringDirectionalInput.Remove(Helper.ToVkKey(placeholderVk));
                    }
                }
            }
            if (isDirectionalUp)
            {
                foreach (var skillKey in _suspendedSkillsDuringDirectionalInput)
                {
                    var sideButton = SideMouseBindings.Button(skillKey);
                    if (sideButton != 0)
                    {
                        if (_heldSideButtons.TryGetValue(sideButton, out var heldSide)) _ = RestoreSidePress(heldSide);
                        continue;
                    }
                    var mappedKey = _swapMap.FirstOrDefault(kvp => kvp.Value == Helper.ToWinFormsKey(skillKey)).Key;
                    if (mappedKey == VirtualKeyCode.F23)
                    {
                        InjectDirectionalRestoreKeyDown(LeftKey);
                    }
                    else if (mappedKey == VirtualKeyCode.F24)
                    {
                        InjectDirectionalRestoreKeyDown(RightKey);
                    }
                    else if (mappedKey == VirtualKeyCode.F22)
                    {
                        InjectDirectionalRestoreKeyDown(MiddleKey);
                    }
                    else
                    {
                        InjectDirectionalRestoreKeyDown(mappedKey);
                    }
                    
                }
            }
            
            if (!generation.HasValue || _outputSources.Up(physicalVk, sidePress?.SourceId ?? (int)placeholderVk))
                InjectLiteralKeyUp(physicalVk);
            
        }
        /// <summary>
        /// Restores a suspended side-button output only while that same press is still held.
        /// </summary>
        private async Task RestoreSidePress(SideButtonPress press)
        {
            if (!_pendingDirectionalRestoreKeys.Contains(press.Output)) return;
            await Task.Delay(40);
            _pendingDirectionalRestoreKeys.Remove(press.Output);
            if (press.Generation == _bindingGeneration
                && _heldSideButtons.TryGetValue(press.Button, out var current) && ReferenceEquals(press, current))
                InjectKey(press.Output, 0, Win32.DirectionalRestoreInjectionTag);
        }

        private async Task HandleWASDKeyDown(VirtualKeyCode physicalVk)
        {
            if (SkipLogic)
            {
                return;
            }
            var op = Application.Current.Dispatcher.InvokeAsync(
                () => _controllerManager.HandleKeyDownAsync(
                    KeyInterop.KeyFromVirtualKey((int)physicalVk), AltMode)
            );

            var inner = await op;     

            await inner;
        }

      
        
        /// <summary>
        /// Forwards a raw WASD key-up into the controller state machine.
        /// </summary>
        private async Task HandleWASDKeyUp(VirtualKeyCode physicalVk)
        {
            if (SkipLogic)
            {
                return;
            }

            var op = Application.Current.Dispatcher.InvokeAsync(
                () => _controllerManager.HandleKeyUpAsync(
                    KeyInterop.KeyFromVirtualKey((int)physicalVk), AltMode)
            );

            var inner = await op;     

            await inner;
        }

        /// <summary>
        /// Handles Win32 hotkey messages registered against the settings window.
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_idToKey.TryGetValue(id, out var vk))
                {
                    if (!_pressedKeys.Contains(vk))
                    {
                        _pressedKeys.Add(vk);
                        _callbacks[id]?.Invoke();
                    }
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

      
        /// <summary>
        /// Processes low-level keyboard traffic for remapping, placeholder injection, and midpoint editing.
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
        const int WM_SYSKEYDOWN = 0x0104;
        const int WM_SYSKEYUP = 0x0105;
        

        if (nCode >= 0)
        {
            var info             = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
            var vk               = (VirtualKeyCode)info.vkCode;
            int  msg              = wParam.ToInt32();
            var isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            var isUp   = msg == WM_KEYUP   || msg == WM_SYSKEYUP;
            
            var didRun = ExecuteAnySettingsHotkey(vk, isDown);
            if (!_toggledOff && !didRun)
            {

                bool injectedByPT = info.flags.HasFlag(Win32.KBDLLHOOKSTRUCTFlags.LLKHF_INJECTED);
                bool injectedByDirectionalRestore = info.dwExtraInfo == new UIntPtr(Win32.DirectionalRestoreInjectionTag);
                bool injectedByPlaceholder = info.dwExtraInfo == new UIntPtr(Win32.PlaceholderInjectionTag);
                bool injectedByLiteral = info.dwExtraInfo == new UIntPtr(Win32.LiteralInjectionTag);

                if (!injectedByPT && !injectedByPlaceholder && !injectedByLiteral && !injectedByDirectionalRestore
                    && _swapMap.ContainsKey(vk))
                {
                    var mapped = _swapMap[vk];
                    if (isDown && _physicalPressed.Add(vk))
                    {
                        InjectPlaceholderKeyDown(mapped);
                    }
                    else if (isUp && _physicalPressed.Remove(vk))
                    {
                        InjectPlaceholderKeyUp(mapped);
                    }
                    return (IntPtr)1; 
                }

                if (injectedByPlaceholder && _swapMap.Values.Contains(vk))
                {
                    if (isDown && _skillDownCallbacks.TryGetValue(vk, out var downAct))
                        downAct();
                    if (isUp   && _releaseCallbacks  .TryGetValue(vk, out var upAct))
                        upAct();

                    return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                }

                if (injectedByLiteral && _swapMap.ContainsKey(vk))
                {
                    if (isDown && _literalDownCallbacks.TryGetValue(vk, out var litDown))
                        litDown();
                    if (isUp   && _literalUpCallbacks  .TryGetValue(vk, out var litUp))
                        litUp();

                    return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                }
                
                if (!injectedByPlaceholder && !injectedByLiteral && !_swapMap.ContainsKey(vk))
                {
                    if (isDown && _skillDownCallbacks.TryGetValue(vk, out var normDown))
                        normDown();
                    if (isUp  && _releaseCallbacks  .TryGetValue(vk, out var normUp))
                        normUp();

                    return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                }
            }
            else
            {
                if (vk == VirtualKeyCode.LEFT && (_viewModel.OverlayEnabled || _viewModel.EnableSetMidpoint))
                {
                    if (_viewModel.EnableSetMidpoint && !_viewModel.OverlayEnabled)
                    {
                        var newMidX = _viewModel.MidPoint.X - 2;
                        var newMidY = _viewModel.MidPoint.Y;
                        
                        _viewModel.MidPoint = new Win32.POINT { X = newMidX, Y = newMidY };
                        Helper.SetCursorPosDip(newMidX, newMidY);
                        return (IntPtr)1; 
                        
                    }
                    else if (_viewModel.OverlayEnabled)
                    {
                        _viewModel.XCursorCenter -= 2;
                        Helper.SetCursorPosDip(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
                        return (IntPtr)1; 
                    }
                    return (IntPtr)1; 
                }
                else if (vk == VirtualKeyCode.RIGHT && (_viewModel.OverlayEnabled || _viewModel.EnableSetMidpoint))
                {
                    if (_viewModel.EnableSetMidpoint  && !_viewModel.OverlayEnabled)
                    {
                        var newMidX = _viewModel.MidPoint.X + 2;
                        var newMidY = _viewModel.MidPoint.Y;
                        
                        _viewModel.MidPoint = new Win32.POINT { X = newMidX, Y = newMidY };
                        Helper.SetCursorPosDip(newMidX, newMidY);

                        return (IntPtr)1; 
                    }
                    else if (_viewModel.OverlayEnabled)
                    {
                        _viewModel.XCursorCenter += 2;
                        Helper.SetCursorPosDip(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
                        return (IntPtr)1; 
                    }
                    return (IntPtr)1; 
                }
                else if (vk == VirtualKeyCode.UP && (_viewModel.OverlayEnabled || _viewModel.EnableSetMidpoint))
                {
                    if (_viewModel.EnableSetMidpoint  && !_viewModel.OverlayEnabled)
                    {
                        var newMidX = _viewModel.MidPoint.X;
                        var newMidY = _viewModel.MidPoint.Y - 2;
                        
                        _viewModel.MidPoint = new Win32.POINT { X = newMidX, Y = newMidY };
                        Helper.SetCursorPosDip(newMidX, newMidY);

                        return (IntPtr)1; 
                    }
                    else if (_viewModel.OverlayEnabled)
                    {
                        _viewModel.YCursorCenter -= 2;
                        Helper.SetCursorPosDip(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
                        return (IntPtr)1; 
                    }
                    return (IntPtr)1; 
                }
                else if (vk == VirtualKeyCode.DOWN && (_viewModel.OverlayEnabled || _viewModel.EnableSetMidpoint))
                {
                    if (_viewModel.EnableSetMidpoint  && !_viewModel.OverlayEnabled)
                    {
                        var newMidX = _viewModel.MidPoint.X;
                        var newMidY = _viewModel.MidPoint.Y + 2;
                        
                        _viewModel.MidPoint = new Win32.POINT { X = newMidX, Y = newMidY };
                        Helper.SetCursorPosDip(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
                        Helper.SetCursorPosDip(newMidX, newMidY);


                        return (IntPtr)1; 
                    }
                    else if (_viewModel.OverlayEnabled)
                    {
                        _viewModel.YCursorCenter += 2;
                        return (IntPtr)1; 
                    }
                    return (IntPtr)1; 
                }
            }
            
        }
        
        return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

        /// <summary>
        /// Executes settings-level hotkeys before normal remapping logic runs.
        /// </summary>
        private bool ExecuteAnySettingsHotkey(VirtualKeyCode vk, bool isDown)
{
    if (!isDown) return false;
    if (vk == _viewModel.ToggleVisualCursorKey.VirtualKey)
    {
        _viewModel.ToggleVisualCursorCommand.Execute(null);

        return true;

    }
    else if (vk == _viewModel.SetMidpointKey.VirtualKey)
    {
        _viewModel.SetMidPointCommand.Execute(null);
        return true;

    }
    else if (vk == _viewModel.TeleportMidpointKey.VirtualKey)
    {
        if (_viewModel.EnableSetMidpoint || _viewModel.OverlayEnabled)
        {
            _viewModel.TeleportMidPointCommand.Execute(null);
            return true; 
        }
    }
    else if (vk == _viewModel.ApplyKey.VirtualKey)
    {
        _viewModel.ApplyCommand.Execute(null);
        return true;

    }
    else if (vk == _viewModel.CenterOverlayKey.VirtualKey)
    {
        _viewModel.CenterOverlayCommand.Execute(null);
        return true;
    }
    else if (vk == _viewModel.EnableSetMidpointKey.VirtualKey)
    {
        _viewModel.EnableMidpointCommand.Execute(null);
        return true;
    }
    else if (vk == _viewModel.ToggleOverlayKey.VirtualKey)
    {
        _viewModel.ToggleOverlayCommand.Execute(null);
        return true;

    }
    else if (vk == _viewModel.HoldToggleVisualCursorKey.VirtualKey)
    {
        _viewModel.HoldToggleVisualCursorCommand.Execute(null);
        return true;

    }

    return false;
}

        /// <summary>
        /// Unregisters hotkeys and removes the low-level keyboard hook.
        /// </summary>
        public void Dispose()
        {
            SideButtonsEnabled = false;
            ReleaseActiveInputs();
            foreach (var id in _callbacks.Keys)
                Win32.UnregisterHotKey(_hwndSource.Handle, id);

            _hwndSource.RemoveHook(WndProc);

            Win32.UnhookWindowsHookEx(_keyboardHookId);
        }

        
    }
}
