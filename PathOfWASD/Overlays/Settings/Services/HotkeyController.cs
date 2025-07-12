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

    public class HotkeyController : IDisposable
    {
        private readonly HashSet<VirtualKeyCode> _physicalPressed = new();
        private readonly ControllerManager _controllerManager;
        
        private HwndSource _hwndSource;
        private readonly SettingsViewModel _viewModel;
        private readonly Dictionary<int, Action> _callbacks = new();
        private readonly Dictionary<VirtualKeyCode, Action> _releaseCallbacks = new();
        private int _nextId = 0;

        private readonly Dictionary<int, VirtualKeyCode> _idToKey = new();
        private readonly Dictionary<VirtualKeyCode, Action> _literalDownCallbacks = new();
        private readonly Dictionary<VirtualKeyCode, Action> _literalUpCallbacks   = new();
        private readonly HashSet<VirtualKeyCode> _pressedKeys = new();
        public bool SkipLogic { get; set; } = false;
        private Win32.LowLevelKeyboardProc _keyboardProc;
        private IntPtr _keyboardHookId;

        private List<Key> currentSkillsHeldDownDuringDirectionDown = new();
        private readonly Dictionary<VirtualKeyCode, Action> _skillDownCallbacks = new();

        private readonly Dictionary<VirtualKeyCode, VirtualKeyCode> _swapMap
            = new Dictionary<VirtualKeyCode, VirtualKeyCode>();
        private readonly Dictionary<VirtualKeyCode, TaskCompletionSource<bool>> _releaseTcs
            = new Dictionary<VirtualKeyCode, TaskCompletionSource<bool>>();
        private List<VirtualKeyCode> womboKeysUp = new List<VirtualKeyCode>();
        private void InjectTaggedKeyDown(VirtualKeyCode vk)   => InjectKey(vk, 0, Win32.MY_TAG);
        private void InjectTaggedKeyUp  (VirtualKeyCode vk)   => InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.MY_TAG);
        private void InjectWomboKeyUp  (VirtualKeyCode vk)
        {
            if (!womboKeysUp.Contains(vk))
            {

                womboKeysUp.Add(vk);
                InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.MY_WOMBO);
            }
        }

        private void InjectLiteralKeyDown(VirtualKeyCode vk)  => InjectKey(vk, 0, Win32.LITERAL_TAG);
        private async Task InjectWomboKeyDown(VirtualKeyCode vk)
        {
            if (womboKeysUp.Contains(vk))
            {
                await Task.Delay(40);
                womboKeysUp.Remove(vk);
                InjectKey(vk, 0, Win32.MY_WOMBO);

            }
            else
            {
            }
        }

        private void InjectLiteralKeyUp  (VirtualKeyCode vk)  => InjectKey(vk, Win32.KEYEVENTF_KEYUP, Win32.LITERAL_TAG);

        private void InjectKey(VirtualKeyCode vk, uint flags, ulong tag)
        {
            if (tag == Win32.LITERAL_TAG)
            {
            }
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

        public HotkeyController(Window window, SettingsViewModel viewModel, ControllerManager controllerManager)
        {
            _sim = new InputSimulator();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));

            if (window == null) throw new ArgumentNullException(nameof(window));
            window.SourceInitialized += OnSourceInitialized; 
        }
        
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
        
        public void Rebind()
        {
            foreach (var id in _callbacks.Keys.ToList())
                Win32.UnregisterHotKey(_hwndSource.Handle, id);
            _callbacks.Clear();
            _idToKey.Clear();
            _nextId = 0;

            _releaseCallbacks.Clear();

            _skillDownCallbacks.Clear();

            _pressedKeys.Clear();

            BindAll();
        }
        

        public void RebindOnToOffWASDMode()
        {
            foreach (var id in _callbacks.Keys.ToList())
                Win32.UnregisterHotKey(_hwndSource.Handle, id);
            _callbacks.Clear();
            _idToKey.Clear();
            _nextId = 0;

            _releaseCallbacks.Clear();

            _skillDownCallbacks.Clear();

            _pressedKeys.Clear();

            BindAll(true);
        }

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
        }
        
        private bool AltMode => _viewModel.InvertAltMode ? Helper.IsKeyDown(Helper.ToVkKey(_viewModel.HoldToggleAltKey.VirtualKey)) : !Helper.IsKeyDown(Helper.ToVkKey(_viewModel.HoldToggleAltKey.VirtualKey));
        
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
            

            foreach (var wpfKey in new[] { Key.W, Key.A, Key.S, Key.D })
            {
                _skillDownCallbacks[Helper.ToWinFormsKey(wpfKey)] = () =>
                {
                    _ = HandleWASDKeyDown(Helper.ToWinFormsKey(wpfKey));
                };
                _releaseCallbacks[Helper.ToWinFormsKey(wpfKey)] = () =>
                {
                    _ = HandleWASDKeyUp(Helper.ToWinFormsKey(wpfKey));
                };
            }
        }

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


private async Task HandleMappedKeyDown(VirtualKeyCode physicalVk, VirtualKeyCode? placeholderVk = null, bool isClick = false)
{

    
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
        await HandleMappedKeyUp(physicalVk, placeholderVk.Value, isClick);
        return;
    }

    await Task.Delay(8);
    var w = _controllerManager.DirectionalKeys.Contains(Helper.ToVkKey( placeholderVk.Value));
    
    if (_controllerManager.State.AnyOtherWASDIsCurrentlyHeldDown && w)
    {
        currentSkillsHeldDownDuringDirectionDown = _controllerManager.State.AllHeldSkillDown(_controllerManager.ToggleKeys);

        foreach (var skillKey in currentSkillsHeldDownDuringDirectionDown)
        {
            var mappedKey = _swapMap.FirstOrDefault(kvp => kvp.Value == Helper.ToWinFormsKey( skillKey)).Key;
            if (mappedKey == VirtualKeyCode.F23)
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(LeftKey)))
                {
                    currentSkillsHeldDownDuringDirectionDown.Remove(skillKey);
                }
                else
                {
                    InjectWomboKeyUp(LeftKey);
                }
                
            }
            else if (mappedKey == VirtualKeyCode.F24)
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(RightKey)))
                {
                    currentSkillsHeldDownDuringDirectionDown.Remove(skillKey);
                }
                else
                {
                    InjectWomboKeyUp(RightKey);
                }
            }
            else if (mappedKey == VirtualKeyCode.F22)
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(MiddleKey)))
                {
                    currentSkillsHeldDownDuringDirectionDown.Remove(skillKey);
                }
                else
                {
                    InjectWomboKeyUp(MiddleKey);
                }
            }
            else
            {
                if (!Helper.IsKeyDown(Helper.ToVkKey(mappedKey)))
                {
                    currentSkillsHeldDownDuringDirectionDown.Remove(skillKey);
                }
                else
                {
                    InjectWomboKeyUp(mappedKey);
                }
            }

        }
    }

    var op = Application.Current.Dispatcher.InvokeAsync(
        () => _controllerManager.HandleKeyDownAsync(
            KeyInterop.KeyFromVirtualKey((int)placeholderVk), isClick && AltMode)
    );
    var inner = await op; 
    await inner;         
    
    await Task.Delay(24);


    InjectLiteralKeyDown(physicalVk);




    await tcs.Task;
    

    await Task.Delay(8);


    await HandleMappedKeyUp(physicalVk, placeholderVk.Value, isClick);
}


        
        private async Task HandleMappedKeyUp(VirtualKeyCode physicalVk, VirtualKeyCode placeholderVk, bool isClick)
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
                    KeyInterop.KeyFromVirtualKey((int)placeholderVk), isClick && AltMode)
            );

            var inner = await op;      

            await inner;

            if (!isDirectionalUp)
            {
                if (currentSkillsHeldDownDuringDirectionDown.Any())
                {
                    if (currentSkillsHeldDownDuringDirectionDown.Contains(Helper.ToVkKey(placeholderVk)))
                    {
                        currentSkillsHeldDownDuringDirectionDown.Remove(Helper.ToVkKey( placeholderVk));
                    }
                }
            }
            if (isDirectionalUp)
            {
                foreach (var q in currentSkillsHeldDownDuringDirectionDown)
                {
                    var mappedKey = _swapMap.FirstOrDefault(kvp => kvp.Value == Helper.ToWinFormsKey( q)).Key;
                    if (mappedKey == VirtualKeyCode.F23)
                    {
                        InjectWomboKeyDown(LeftKey);
                    }
                    else if (mappedKey == VirtualKeyCode.F24)
                    {
                        InjectWomboKeyDown(RightKey);
                    }
                    else if (mappedKey == VirtualKeyCode.F22)
                    {
                        InjectWomboKeyDown(MiddleKey);
                    }
                    else
                    {
                        InjectWomboKeyDown(mappedKey);
                    }
                    
                }
            }
            
            InjectLiteralKeyUp(physicalVk);
            
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

                bool injectedByPT      = info.flags.HasFlag(Win32.KBDLLHOOKSTRUCTFlags.LLKHF_INJECTED);
                bool injectedByWombo      = info.dwExtraInfo == new UIntPtr(Win32.MY_WOMBO);

                bool injectedByUs      = info.dwExtraInfo == new UIntPtr(Win32.MY_TAG);
                bool injectedByLiteral = info.dwExtraInfo == new UIntPtr(Win32.LITERAL_TAG);

                if (!injectedByPT && !injectedByUs && !injectedByLiteral && !injectedByWombo
                    && _swapMap.ContainsKey(vk))
                {
                    var mapped = _swapMap[vk];
                    if (isDown && _physicalPressed.Add(vk))
                    {
                        InjectTaggedKeyDown(mapped);
                    }
                    else if (isUp)
                    {
                        InjectTaggedKeyUp(mapped);
                        _physicalPressed.Remove(vk);
                    }
                    return (IntPtr)1; 
                }

                if (injectedByUs && _swapMap.Values.Contains(vk))
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
                
                if (!injectedByUs && !injectedByLiteral && !_swapMap.ContainsKey(vk))
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
                        Win32.SetCursorPos(newMidX, newMidY);
                        return (IntPtr)1;  
                        
                    }
                    else if (_viewModel.OverlayEnabled)
                    {
                        _viewModel.XCursorCenter -= 2;
                        Win32.SetCursorPos(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
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
                        Win32.SetCursorPos(newMidX, newMidY);

                        return (IntPtr)1; 
                    }
                    else if (_viewModel.OverlayEnabled)
                    {
                        _viewModel.XCursorCenter += 2;
                        Win32.SetCursorPos(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
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
                        Win32.SetCursorPos(newMidX, newMidY);

                        return (IntPtr)1;  
                    }
                    else if (_viewModel.OverlayEnabled)
                    {
                        _viewModel.YCursorCenter -= 2;
                        Win32.SetCursorPos(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
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
                        Win32.SetCursorPos(_viewModel.MidPoint.X, _viewModel.MidPoint.Y);
                        Win32.SetCursorPos(newMidX, newMidY);


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

public void Dispose()
        {
            foreach (var id in _callbacks.Keys)
                Win32.UnregisterHotKey(_hwndSource.Handle, id);

            _hwndSource.RemoveHook(WndProc);

            Win32.UnhookWindowsHookEx(_keyboardHookId);
        }

        
    }
}
