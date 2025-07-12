using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Windows.Input;
using PathOfWASD.Helpers;
using PathOfWASD.Internals;
using PathOfWASD.Overlays.Settings.Models;
using WindowsInput.Native;

namespace PathOfWASD.Overlays.Settings.ViewModels
{
   public class Settings : ISettingService
    {
        private static readonly string AppDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PathOfWASD"
            );
        private const string SettingsFileNameOnly = "settings.json";
        private static readonly string SettingsFileName =
            Path.Combine(AppDirectory, SettingsFileNameOnly);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        public CursorMode CursorMode { get; set; }

        public VirtualKeyCode MovementKey { get; set; }
        public VirtualKeyCode StandKey { get; set; }
        public VirtualKeyCode ToggleOverlayKey { get; set; }
        public VirtualKeyCode ToggleVisualCursorKey { get; set; }
        public VirtualKeyCode HoldToggleVisualCursorKey { get; set; }
        public VirtualKeyCode HoldToggleAltKey { get; set; }
        public VirtualKeyCode SetMidpointKey { get; set; }
        public VirtualKeyCode TeleportMidpointKey { get; set; }
        public VirtualKeyCode ApplyKey { get; set; }
        public VirtualKeyCode CenterOverlayKey { get; set; }
        public VirtualKeyCode EnableSetMidpointKey { get; set; }
        public VirtualKeyCode LeftKey { get; set; }
        public VirtualKeyCode RightKey { get; set; }
        public VirtualKeyCode MiddleKey { get; set; }

        public int MovementOffset { get; set; }
        public int XCursorCenter { get; set; }
        public int YCursorCenter { get; set; }
        public int CursorSize { get; set; }
        public int AppWidthSize { get; set; }
        public int AppHeightSize { get; set; }
        public int AfterMouseJumpDelayOffset { get; set; }
        public int BeforeMoveDelayOffset { get; set; }
        public double Sensitivity { get; set; } = 1000; 
        public bool EnableLeftClickInteractions { get; set; }
        public bool InvertAltMode { get; set; }
        public bool EnableLeftClickMovement { get; set; }
        public POINT MidPoint { get; set; }

        public VirtualKeyCode[] KeysToMapToToggleEntries { get; set; }
        public VirtualKeyCode[] KeysToMapToToggleDirectionalEntries { get; set; }
        
        
        [JsonConstructor]
        public Settings() { }

        private void InitializeDefaults()
        {
            CursorMode                = CursorMode.AlwaysHide;
            MovementKey               = VirtualKeyCode.INSERT;
            StandKey                  = VirtualKeyCode.DELETE;
            ToggleOverlayKey          = VirtualKeyCode.F1;
            ToggleVisualCursorKey     = VirtualKeyCode.F2;
            HoldToggleVisualCursorKey = VirtualKeyCode.LMENU;
            HoldToggleAltKey = VirtualKeyCode.SCROLL;
            SetMidpointKey            = VirtualKeyCode.F3;
            TeleportMidpointKey = VirtualKeyCode.F7;
            ApplyKey                  = VirtualKeyCode.F12;
            CenterOverlayKey = VirtualKeyCode.F8;
            EnableSetMidpointKey = VirtualKeyCode.F6;
            LeftKey                    = VirtualKeyCode.NUMPAD8;
            RightKey = VirtualKeyCode.NUMPAD9;
            MiddleKey = VirtualKeyCode.NUMPAD7;
            EnableLeftClickInteractions = true;
            InvertAltMode = false;
            EnableLeftClickMovement = false;
            MovementOffset            = 100;
            XCursorCenter = 0;
            CursorSize = 50;
            YCursorCenter = 0;
            AppWidthSize            = 590;
            AppHeightSize = 1082;
            BeforeMoveDelayOffset     = 100;
            Sensitivity               = 1000;
                
            MidPoint = POINT.FromWin32(Helper.CalculateGameMidpoint());

            KeysToMapToToggleEntries  = new[] { VirtualKeyCode.VK_Q, VirtualKeyCode.VK_E, VirtualKeyCode.VK_R, VirtualKeyCode.VK_T };
            KeysToMapToToggleDirectionalEntries  = new[] { VirtualKeyCode.VK_F };
        }

        public Settings Load()
        {
            try { Directory.CreateDirectory(AppDirectory); }
            catch (Exception ex) { Console.WriteLine($"Failed to create settings directory: {ex.Message}"); }

            if (File.Exists(SettingsFileName))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFileName);
                    var sevenFucks = JsonSerializer.Deserialize<Settings>(json, JsonOptions)
                        ?? CreateAndSaveDefaults();
                    return sevenFucks;
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.Delete(SettingsFileName);
                    }
                    catch (Exception deleteEx)
                    {
                        Console.WriteLine($"Failed to delete corrupted settings file: {deleteEx.Message}");
                    }
                    return CreateAndSaveDefaults();
                }
            }
            else
            {
                return CreateAndSaveDefaults();
            }
        }
        public Settings Clone()
        {
            return new Settings
            {
                CursorMode = CursorMode,
                MovementKey = MovementKey,
                StandKey = StandKey,
                ToggleOverlayKey = ToggleOverlayKey,
                ToggleVisualCursorKey = ToggleVisualCursorKey,
                HoldToggleVisualCursorKey = HoldToggleVisualCursorKey,
                HoldToggleAltKey = HoldToggleAltKey,
                SetMidpointKey = SetMidpointKey,
                TeleportMidpointKey = TeleportMidpointKey,
                ApplyKey = ApplyKey,
                CenterOverlayKey = CenterOverlayKey,
                EnableSetMidpointKey = EnableSetMidpointKey,
                LeftKey = LeftKey,
                RightKey = RightKey,
                MiddleKey = MiddleKey,
                MovementOffset = MovementOffset,
                XCursorCenter = XCursorCenter,
                YCursorCenter = YCursorCenter,
                CursorSize = CursorSize,
                AppHeightSize = AppHeightSize,
                AppWidthSize = AppWidthSize,
                AfterMouseJumpDelayOffset = AfterMouseJumpDelayOffset,
                BeforeMoveDelayOffset = BeforeMoveDelayOffset,
                EnableLeftClickInteractions = EnableLeftClickInteractions,
                EnableLeftClickMovement = EnableLeftClickMovement,
                InvertAltMode = InvertAltMode,
                Sensitivity = Sensitivity,
                MidPoint = MidPoint,
                KeysToMapToToggleEntries = KeysToMapToToggleEntries,
                KeysToMapToToggleDirectionalEntries = KeysToMapToToggleDirectionalEntries
            };
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Settings other) return false;

            return CursorMode == other.CursorMode &&
                   MovementKey == other.MovementKey &&
                   StandKey == other.StandKey &&
                   ToggleOverlayKey == other.ToggleOverlayKey &&
                   ToggleVisualCursorKey == other.ToggleVisualCursorKey &&
                   HoldToggleVisualCursorKey == other.HoldToggleVisualCursorKey &&
                   HoldToggleAltKey == other.HoldToggleAltKey &&
                   SetMidpointKey == other.SetMidpointKey &&
                   TeleportMidpointKey == other.TeleportMidpointKey &&
                   ApplyKey == other.ApplyKey &&
                   CenterOverlayKey == other.CenterOverlayKey &&
                   EnableSetMidpointKey == other.EnableSetMidpointKey &&
                   LeftKey == other.LeftKey &&
                   RightKey == other.RightKey &&
                   MiddleKey == other.MiddleKey &&
                   MovementOffset == other.MovementOffset &&
                   AfterMouseJumpDelayOffset == other.AfterMouseJumpDelayOffset &&
                   BeforeMoveDelayOffset == other.BeforeMoveDelayOffset &&
                   EnableLeftClickInteractions == other.EnableLeftClickInteractions &&
                   EnableLeftClickMovement == other.EnableLeftClickMovement &&
                   InvertAltMode == other.InvertAltMode &&
                   Sensitivity == other.Sensitivity &&
                   XCursorCenter == other.XCursorCenter &&
                   YCursorCenter == other.YCursorCenter &&
                   CursorSize == other.CursorSize &&
                   KeysToMapToToggleEntries.SequenceEqual(other.KeysToMapToToggleEntries) &&
                   KeysToMapToToggleDirectionalEntries.SequenceEqual(other.KeysToMapToToggleDirectionalEntries);
        }

        public override int GetHashCode() => base.GetHashCode();
        private static Settings CreateAndSaveDefaults()
        {
            var s = new Settings();
            s.InitializeDefaults();
            s.Save();
            return s;
        }
        
        public static Settings Defaults { get; } = new Settings
        {
            CursorMode = CursorMode.AlwaysHide,
            MovementKey = VirtualKeyCode.INSERT,
            StandKey = VirtualKeyCode.DELETE,
            ToggleOverlayKey = VirtualKeyCode.F1,
            ToggleVisualCursorKey = VirtualKeyCode.F2,
            HoldToggleVisualCursorKey = VirtualKeyCode.SCROLL,
            HoldToggleAltKey = VirtualKeyCode.LMENU,
            SetMidpointKey = VirtualKeyCode.F3,
            TeleportMidpointKey = VirtualKeyCode.F7,
            ApplyKey = VirtualKeyCode.F12,
            CenterOverlayKey = VirtualKeyCode.F8,
            EnableSetMidpointKey = VirtualKeyCode.F6,
            LeftKey = VirtualKeyCode.NUMPAD8,
            RightKey = VirtualKeyCode.NUMPAD9,
            MiddleKey = VirtualKeyCode.NUMPAD7,
            MovementOffset = 100,
            XCursorCenter = 0,
            YCursorCenter = 0,
            CursorSize = 50,
            AppHeightSize = 1082,
            AppWidthSize = 590,
            AfterMouseJumpDelayOffset = 30,
            BeforeMoveDelayOffset = 100,
            EnableLeftClickInteractions = true,
            EnableLeftClickMovement = false,
            InvertAltMode = false,
            Sensitivity = 1000,
            MidPoint = new POINT
            {
                X = Helper.CalculateGameMidpoint().X,
                Y = Helper.CalculateGameMidpoint().Y
            },
            KeysToMapToToggleEntries  = new[] { VirtualKeyCode.VK_Q, VirtualKeyCode.VK_E, VirtualKeyCode.VK_R, VirtualKeyCode.VK_T },
            KeysToMapToToggleDirectionalEntries  = new[] { VirtualKeyCode.VK_F }
        };
        
        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public void Save(Settings settings)
        {
            try
            {
                var modSettings = settings;
                Directory.CreateDirectory(AppDirectory);
                var json = JsonSerializer.Serialize(modSettings, JsonOptions);
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings statically: {ex.Message}");
            }
        }
    }

    public class POINT
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Win32.POINT ToWin32() => new Win32.POINT { X = X, Y = Y };

        public static POINT FromWin32(Win32.POINT p) => new POINT { X = p.X, Y = p.Y };
    }
}