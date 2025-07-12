using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfWASD.Helpers;
using PathOfWASD.Internals;
using PathOfWASD.Overlays.BGFunctionalities;
using PathOfWASD.Overlays.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Models;
using WindowsInput.Native;

namespace PathOfWASD.Overlays.Settings.ViewModels
{
   public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingService _settingsService;
        private readonly ICursorImageLoader _imageLoader;
        private Settings _settings;
        private Settings _lastAppliedSettings;
        
        [ObservableProperty]
        private bool _hasUnappliedChanges;
        [ObservableProperty]
        private CursorMode _cursorMode;
        
        public int AppHeightSize { get; set; }

        public int AppWidthSize { get; set; }
        [ObservableProperty] 
        private int xCursorCenter;
        partial void OnXCursorCenterChanged(int oldValue, int newValue)
            => CursorCenterChanged?.Invoke();
        
        [ObservableProperty]
        private int yCursorCenter;
        partial void OnYCursorCenterChanged(int oldValue, int newValue)
            => CursorCenterChanged?.Invoke();
        
        [ObservableProperty] 
        private int cursorSize;
        partial void OnCursorSizeChanged(int width, int height)
            => CursorSizeChanged?.Invoke();
        
        [ObservableProperty]
        private KeyPair centerOverlayKey = new KeyPair();
        
        [ObservableProperty]
        private KeyPair enableSetMidpointKey = new KeyPair();
        
        [ObservableProperty]
        private KeyPair movementKey = new KeyPair();

        [ObservableProperty]
        private KeyPair standKey     = new KeyPair();

        [ObservableProperty]
        private KeyPair toggleOverlayKey     = new KeyPair();

        [ObservableProperty]
        private KeyPair toggleVisualCursorKey = new KeyPair();

        [ObservableProperty]
        private KeyPair holdToggleVisualCursorKey = new KeyPair();

        [ObservableProperty]
        private KeyPair holdToggleAltKey = new KeyPair();

        [ObservableProperty]
        private KeyPair setMidpointKey = new KeyPair();

        [ObservableProperty]
        private KeyPair teleportMidpointKey = new KeyPair();
        
        [ObservableProperty]
        private KeyPair leftKey   = new KeyPair();

        [ObservableProperty]
        private KeyPair rightKey  = new KeyPair();

        [ObservableProperty]
        private KeyPair middleKey = new KeyPair();

        [ObservableProperty]
        private KeyPair applyKey  = new KeyPair();
        
        [ObservableProperty]
        private int _movementOffset;
        [ObservableProperty]
        private int _afterMouseJumpDelayOffset;
        [ObservableProperty]
        private int _beforeMoveDelayOffset;
        [ObservableProperty]
        private bool _enableLeftClickInteractions;
        [ObservableProperty]
        private bool _enableLeftClickMovement;
        [ObservableProperty]
        private bool _invertAltMode;
        [ObservableProperty]
        private bool _enableSetMidpoint;

        [ObservableProperty] 
        private bool _overlayEnabled;
        [ObservableProperty]
        private double _sensitivity;
        [ObservableProperty]
        private Win32.POINT _midPoint;
        partial void OnMidPointChanged(Win32.POINT oldValue, Win32.POINT newValue)
            => CursorCenterChanged?.Invoke();

        partial void OnMidPointChanged(Win32.POINT value)
        {
            OnPropertyChanged(nameof(MidpointXDisplay));
            OnPropertyChanged(nameof(MidpointYDisplay));
        }

        public int MidpointXDisplay => MidPoint.X;
        public int MidpointYDisplay => MidPoint.Y;

        public List<VirtualKeyCode> AvailableVirtualKeys { get; }
        public List<Key> MasterWpfKeys { get; }
        public ObservableCollection<ToggleKeyEntry> ToggleEntries { get; } = new();

        public event Action ApplyRequested;
        public event Action SaveRequested;
        public event Action ToggleOverlayRequested;
        public event Action CenterOverlayRequested;
        public event Action ToggleVisualCursorRequested;
        public event Action HoldToggleVisualCursorRequested;
        public event Action? CursorCenterChanged;
        public event Action? CursorSizeChanged;
        public event Action? VirtualCursorChanged;
        public Action OnRequestClose { get; set; }
        private bool _isSwappingKeys;
        private readonly Dictionary<KeyPair, VirtualKeyCode> _lastKeyMap = new();

        public SettingsViewModel(ISettingService settingsService, ICursorImageLoader imageLoader)
{
    _settingsService = settingsService;
    _imageLoader = imageLoader;
    _settings        = _settingsService.Load();
    _lastAppliedSettings = _settings.Clone();

    MasterWpfKeys        = KeyMaps.MasterWpfKeys;
    AvailableVirtualKeys = KeyMaps.AvailableVirtualKeys;

    movementKey.PropertyChanged += OnKeyPairPropertyChanged;
    standKey.PropertyChanged += OnKeyPairPropertyChanged;
    toggleOverlayKey.PropertyChanged += OnKeyPairPropertyChanged;
    toggleVisualCursorKey.PropertyChanged += OnKeyPairPropertyChanged;
    holdToggleVisualCursorKey.PropertyChanged += OnKeyPairPropertyChanged;
    holdToggleAltKey.PropertyChanged += OnKeyPairPropertyChanged;
    setMidpointKey.PropertyChanged += OnKeyPairPropertyChanged;
    teleportMidpointKey.PropertyChanged += OnKeyPairPropertyChanged;
    leftKey.PropertyChanged += OnKeyPairPropertyChanged;
    rightKey.PropertyChanged += OnKeyPairPropertyChanged;
    middleKey.PropertyChanged += OnKeyPairPropertyChanged;
    applyKey.PropertyChanged += OnKeyPairPropertyChanged;
    centerOverlayKey.PropertyChanged += OnKeyPairPropertyChanged;
    enableSetMidpointKey.PropertyChanged += OnKeyPairPropertyChanged;
    foreach (var kp in new[] { movementKey, standKey, toggleOverlayKey, toggleVisualCursorKey,
                 holdToggleVisualCursorKey, holdToggleAltKey, setMidpointKey,
                 teleportMidpointKey, leftKey, rightKey, middleKey, applyKey, centerOverlayKey, enableSetMidpointKey })
    {
        _lastKeyMap[kp] = kp.VirtualKey;
    }
    ToggleEntries.CollectionChanged += OnToggleEntriesChanged;

    foreach (var entry in ToggleEntries)
        entry.PropertyChanged += OnToggleEntryChanged;

    ApplySettings(_settings);
}
        
private void OnToggleEntriesChanged(object _, NotifyCollectionChangedEventArgs e)
{
    if (e.NewItems != null)
    {
        foreach (ToggleKeyEntry entry in e.NewItems)
        {
            entry.PropertyChanged += OnToggleEntryChanged;
            entry.SelectedKey.PropertyChanged += OnToggleEntryChanged;
        }
    }
    if (e.OldItems != null)
    {
        foreach (ToggleKeyEntry entry in e.OldItems)
        {
            entry.PropertyChanged -= OnToggleEntryChanged;
            entry.SelectedKey.PropertyChanged -= OnToggleEntryChanged;
        }
    }
    HasUnappliedChanges = !GetCurrentSettings().Equals(_lastAppliedSettings);
}

private void OnToggleEntryChanged(object sender, PropertyChangedEventArgs e)
{
    HasUnappliedChanges = !GetCurrentSettings().Equals(_lastAppliedSettings);
}
        public void UpdateAppSize(int width, int height)
        {
            AppWidthSize = width;
            AppHeightSize = height;
        }
        
        private void ApplySettings(Settings s, POINT? oldpoint = null)
        {
            CursorMode = s.CursorMode;
            MovementKey.VirtualKey               = s.MovementKey;
            StandKey.VirtualKey                  = s.StandKey;
            ToggleOverlayKey.VirtualKey          = s.ToggleOverlayKey;
            ToggleVisualCursorKey.VirtualKey     = s.ToggleVisualCursorKey;
            HoldToggleVisualCursorKey.VirtualKey = s.HoldToggleVisualCursorKey;
            HoldToggleAltKey.VirtualKey          = s.HoldToggleAltKey;
            SetMidpointKey.VirtualKey            = s.SetMidpointKey;
            TeleportMidpointKey.VirtualKey = s.TeleportMidpointKey;
            LeftKey.VirtualKey                   = s.LeftKey;
            RightKey.VirtualKey                  = s.RightKey;
            MiddleKey.VirtualKey = s.MiddleKey;
            ApplyKey.VirtualKey                  = s.ApplyKey;
            CenterOverlayKey.VirtualKey = s.CenterOverlayKey;
            EnableSetMidpointKey.VirtualKey = s.EnableSetMidpointKey;
            MovementOffset = s.MovementOffset;
            XCursorCenter = s.XCursorCenter;
            CursorSize = s.CursorSize;
            YCursorCenter = s.YCursorCenter;
            AppWidthSize = s.AppWidthSize;
            AppHeightSize = s.AppHeightSize;
            AfterMouseJumpDelayOffset = s.AfterMouseJumpDelayOffset;
            BeforeMoveDelayOffset = s.BeforeMoveDelayOffset;
            EnableLeftClickInteractions = s.EnableLeftClickInteractions;
            EnableLeftClickMovement = s.EnableLeftClickMovement;
            InvertAltMode = s.InvertAltMode;
            Sensitivity = s.Sensitivity;
            MidPoint = oldpoint?.ToWin32() ?? s.MidPoint.ToWin32();

            ToggleEntries.Clear();
            foreach (var vk in s.KeysToMapToToggleEntries)
            {
                ToggleEntries.Add(new ToggleKeyEntry(vk));
            }

            foreach (var vk in s.KeysToMapToToggleDirectionalEntries)
            {
                ToggleEntries.Add(new ToggleKeyEntry(vk, isDirectional: true));
            }
            
            HasUnappliedChanges = false;
        }
        
        [RelayCommand]
        private void AddToggleEntry()
        {
            var initial = AvailableVirtualKeys.First();

            var entry = new ToggleKeyEntry(initial);

            ToggleEntries.Add(entry);
        }
        
        [RelayCommand]
        private void OpenLocalFolder()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path  = Path.Combine(local, "PathOfWASD");
        
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            Process.Start(new ProcessStartInfo
            {
                FileName        = path,
                UseShellExecute = true,
                Verb            = "open"
            });
        }
        
        [RelayCommand]
        private void RemoveToggleEntry(ToggleKeyEntry entry) => ToggleEntries.Remove(entry);

        [RelayCommand]
        private void Apply()
        {
            var newSettings = new Settings
            {
                CursorMode = CursorMode,
                MovementKey = MovementKey.VirtualKey,
                StandKey = StandKey.VirtualKey,
                ToggleOverlayKey = ToggleOverlayKey.VirtualKey,
                ToggleVisualCursorKey = ToggleVisualCursorKey.VirtualKey,
                HoldToggleVisualCursorKey = HoldToggleVisualCursorKey.VirtualKey,
                HoldToggleAltKey = HoldToggleAltKey.VirtualKey,
                SetMidpointKey = SetMidpointKey.VirtualKey,
                TeleportMidpointKey = TeleportMidpointKey.VirtualKey,
                ApplyKey = ApplyKey.VirtualKey,
                CenterOverlayKey = CenterOverlayKey.VirtualKey,
                EnableSetMidpointKey = EnableSetMidpointKey.VirtualKey,
                LeftKey = LeftKey.VirtualKey,
                RightKey = RightKey.VirtualKey,
                MiddleKey = MiddleKey.VirtualKey,
                MovementOffset = MovementOffset,
                XCursorCenter = XCursorCenter,
                CursorSize = CursorSize,
                YCursorCenter = YCursorCenter,
                AppWidthSize = AppWidthSize,
                AppHeightSize = AppHeightSize,
                AfterMouseJumpDelayOffset = AfterMouseJumpDelayOffset,
                BeforeMoveDelayOffset = BeforeMoveDelayOffset,
                EnableLeftClickInteractions = EnableLeftClickInteractions,
                EnableLeftClickMovement = EnableLeftClickMovement,
                InvertAltMode = InvertAltMode,
                Sensitivity = Sensitivity,
                MidPoint = POINT.FromWin32(MidPoint),
                KeysToMapToToggleEntries =
                    ToggleEntries
                        .Where(x => !x.IsDirectional)
                        .Select(x => x.SelectedKey.VirtualKey)
                        .ToArray(),

                KeysToMapToToggleDirectionalEntries =
                    ToggleEntries
                        .Where(x => x.IsDirectional)
                        .Select(x => x.SelectedKey.VirtualKey)
                        .ToArray()
            };
            ApplySettings(newSettings);
            SaveRequested.Invoke();
        }

        [RelayCommand]
        private void Save()
        {
            var newSettings = new Settings
            {
                CursorMode = CursorMode,
                MovementKey =  MovementKey.VirtualKey,
                StandKey =  StandKey.VirtualKey,
                ToggleOverlayKey =  ToggleOverlayKey.VirtualKey,
                ToggleVisualCursorKey =  ToggleVisualCursorKey.VirtualKey,
                HoldToggleVisualCursorKey =  HoldToggleVisualCursorKey.VirtualKey,
                HoldToggleAltKey =  HoldToggleAltKey.VirtualKey,
                SetMidpointKey =  SetMidpointKey.VirtualKey,
                TeleportMidpointKey = TeleportMidpointKey.VirtualKey,
                ApplyKey =   ApplyKey.VirtualKey,
                CenterOverlayKey = CenterOverlayKey.VirtualKey,
                EnableSetMidpointKey = EnableSetMidpointKey.VirtualKey,
                LeftKey =  LeftKey.VirtualKey,
                RightKey =  RightKey.VirtualKey,
                MiddleKey = MiddleKey.VirtualKey,
                MovementOffset = MovementOffset,
                XCursorCenter = XCursorCenter,
                YCursorCenter = YCursorCenter,
                CursorSize = CursorSize,
                AppWidthSize = AppWidthSize,
                AppHeightSize = AppHeightSize,
                AfterMouseJumpDelayOffset = AfterMouseJumpDelayOffset,
                BeforeMoveDelayOffset = BeforeMoveDelayOffset,
                EnableLeftClickInteractions = EnableLeftClickInteractions,
                EnableLeftClickMovement = EnableLeftClickMovement,
                InvertAltMode = InvertAltMode,
                Sensitivity = Sensitivity,
                MidPoint = POINT.FromWin32(MidPoint),
                KeysToMapToToggleEntries =
                    ToggleEntries
                        .Where(x => !x.IsDirectional)
                        .Select(x => x.SelectedKey.VirtualKey)
                        .ToArray(),

                KeysToMapToToggleDirectionalEntries =
                    ToggleEntries
                        .Where(x => x.IsDirectional)
                        .Select(x => x.SelectedKey.VirtualKey)
                        .ToArray()
            };
            _settingsService.Save(newSettings);
            ApplySettings(newSettings);
            _settings = newSettings;
            _lastAppliedSettings = _settings.Clone(); 
            HasUnappliedChanges = false;
            SaveRequested.Invoke();
        }

        [RelayCommand]
        private void SetMidPoint()
        {
            if (EnableSetMidpoint)
            {
                Win32.GetCursorPos(out var p);
                var dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow);
                MidPoint = new Win32.POINT { X = (int)(p.X / dpi.DpiScaleX), Y = (int)(p.Y / dpi.DpiScaleY) };
            }
        }

        [RelayCommand]
        private void ResetAll() => ApplySettings(_settings);

        [RelayCommand]
        private void ResetAllDefault()
        {
            var oldMidPoint = MidPoint;
            ApplySettings(Settings.Defaults, POINT.FromWin32(oldMidPoint));   
        }
        
        [RelayCommand]
        private void ResetMidPoint()
        {
            if (EnableSetMidpoint)
            {
                var defaultMidpoint = Helper.CalculateGameMidpoint();
                MidPoint = new Win32.POINT
                {
                    X = defaultMidpoint.X,
                    Y = defaultMidpoint.Y
                };
            }
        }
        
        [RelayCommand]
        private void ToggleVisualCursor() => ToggleVisualCursorRequested?.Invoke();

        [RelayCommand]
        private void HoldToggleVisualCursor() => HoldToggleVisualCursorRequested?.Invoke();
        
        [RelayCommand]
        private void ToggleOverlay() => ToggleOverlayRequested?.Invoke();

        [RelayCommand]
        private void CenterOverlay()
        {
            if (OverlayEnabled)
            {
                OverlayEnabled = false;
            }
            else
            {
                OverlayEnabled = true;
            }
            
            CenterOverlayRequested?.Invoke();
        }
        
        [RelayCommand]
        private void UploadCursor()
        {
            try
            {
                VirtualCursorChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Invalid image",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        [RelayCommand]
        private void EnableMidpoint()
        {
            if (!EnableSetMidpoint)
            {
                EnableSetMidpoint = true;
            }
            else
            {
                EnableSetMidpoint = false;
            }
        }

        public bool MidpointEnable { get; set; }

        [RelayCommand]
        private void TeleportMidPoint()
        {
            Win32.SetCursorPos(MidPoint.X, MidPoint.Y);
        }
        
        private Settings GetCurrentSettings()
        {
            return new Settings
            {
                CursorMode = CursorMode,
                MovementKey =  MovementKey.VirtualKey,
                StandKey =  StandKey.VirtualKey,
                ToggleOverlayKey =  ToggleOverlayKey.VirtualKey,
                ToggleVisualCursorKey =  ToggleVisualCursorKey.VirtualKey,
                HoldToggleVisualCursorKey =  HoldToggleVisualCursorKey.VirtualKey,
                HoldToggleAltKey =  HoldToggleAltKey.VirtualKey,
                SetMidpointKey =  SetMidpointKey.VirtualKey,
                TeleportMidpointKey = TeleportMidpointKey.VirtualKey,
                ApplyKey =  ApplyKey.VirtualKey,
                CenterOverlayKey = CenterOverlayKey.VirtualKey,
                EnableSetMidpointKey = EnableSetMidpointKey.VirtualKey,
                LeftKey =  LeftKey.VirtualKey,
                RightKey =  RightKey.VirtualKey,
                MiddleKey = MiddleKey.VirtualKey,
                MovementOffset = MovementOffset,
                XCursorCenter = XCursorCenter,
                YCursorCenter = YCursorCenter,
                CursorSize = CursorSize,
                AppWidthSize = AppWidthSize,
                AppHeightSize = AppHeightSize,
                AfterMouseJumpDelayOffset = AfterMouseJumpDelayOffset,
                BeforeMoveDelayOffset = BeforeMoveDelayOffset,
                EnableLeftClickInteractions = EnableLeftClickInteractions,
                EnableLeftClickMovement = EnableLeftClickMovement,
                InvertAltMode = InvertAltMode,
                Sensitivity = Sensitivity,
                MidPoint =  POINT.FromWin32(MidPoint),
                KeysToMapToToggleEntries =
                    ToggleEntries
                        .Where(x => !x.IsDirectional)
                        .Select(x => x.SelectedKey.VirtualKey)
                        .ToArray(),

                KeysToMapToToggleDirectionalEntries =
                    ToggleEntries
                        .Where(x => x.IsDirectional)
                        .Select(x => x.SelectedKey.VirtualKey)
                        .ToArray()
            };
        }

        private void OnKeyPairPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(KeyPair.VirtualKey)) return;
            if (_isSwappingKeys) return;

            var changedPair = (KeyPair)sender;
            var oldVk = _lastKeyMap.TryGetValue(changedPair, out var prev) ? prev : changedPair.VirtualKey;
            var newVk = changedPair.VirtualKey;

            if (oldVk != newVk)
            {
                var other = _lastKeyMap.Keys.FirstOrDefault(kp => kp != changedPair && kp.VirtualKey == newVk);
                if (other != null)
                {
                    _isSwappingKeys = true;
                    other.VirtualKey = oldVk;
                    _lastKeyMap[other] = oldVk;
                    _isSwappingKeys = false;
                }
                _lastKeyMap[changedPair] = newVk;
            }

            HasUnappliedChanges = !GetCurrentSettings().Equals(_lastAppliedSettings);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName != nameof(HasUnappliedChanges) && e.PropertyName != nameof(EnableSetMidpoint))
                HasUnappliedChanges = !GetCurrentSettings().Equals(_lastAppliedSettings);
        }
    }
}   