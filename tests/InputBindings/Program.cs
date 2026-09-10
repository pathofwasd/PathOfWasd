using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.IO;
using PathOfWASD.Overlays.BGFunctionalities;
using PathOfWASD.Helpers;
using PathOfWASD.Internals;
using PathOfWASD.Managers.Controller;
using PathOfWASD.Managers.Controller.Interfaces;
using PathOfWASD.Overlays.Cursor.Interfaces;
using PathOfWASD.Overlays.Settings.Services;
using PathOfWASD.Overlays.Settings.ViewModels;
using WindowsInput.Native;
using KeyEvent = PathOfWASD.Managers.Controller.KeyEvent;

internal static class Program
{
    private static int _checks;
    [STAThread]
    private static void Main(string[] args)
    {
        _ = new Application();
        if (args.Contains("--render") || args.Contains("--render-conflict")) { RenderSettings(args.Contains("--render-conflict")); return; }
        SettingsChecks();
        LayoutConflictChecks();
        CaptureChecks();
        MovementChecks();
        MouseHookChecks();
        RoutingChecks();
        Console.WriteLine($"PASS: {_checks} input-binding checks. Keyboard output and controller actions were recorded, not sent to Windows.");
    }

    private static void RenderSettings(bool conflict)
    {
        // Load only styles, never App startup or its tray icon / input services.
        var app = XDocument.Load("Startup/App.xaml").Root!;
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var resources = new XElement(wpf + "ResourceDictionary",
            app.Attributes().Where(attribute => attribute.IsNamespaceDeclaration).Select(attribute => new XAttribute(attribute)));
        resources.SetAttributeValue(XNamespace.Xmlns + "bgFunctionalities", "clr-namespace:PathOfWASD.Overlays.BGFunctionalities;assembly=PathOfWASD");
        resources.Add(app.Element(wpf + "Application.Resources")!.Elements().Where(element => (string?)element.Attribute(x + "Key") != "TrayIcon"));
        Application.Current.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
        var store = new MemorySettings();
        var vm = new SettingsViewModel(store, new NoCursorFile());
        var window = new PathOfWASD.Overlays.Settings.Views.SettingsOverlay(vm, store);
        var panes = Descendants(window).OfType<Expander>().Where(expander =>
            Equals(expander.Header, "Movement keys") || expander.Header is TextBlock { Text: "Mapped Mouse Alt Keys" }).ToArray();
        Check(panes.Length == 2, "movement and mouse-alt panes found");
        var stack = new StackPanel { DataContext = vm };
        foreach (var pane in panes) { ((Panel)LogicalTreeHelper.GetParent(pane)).Children.Remove(pane); stack.Children.Add(pane); }
        var root = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), Padding = new Thickness(12), Child = stack };
        root.Measure(new Size(566, double.PositiveInfinity));
        root.Arrange(new Rect(0, 0, 566, root.DesiredSize.Height));
        root.UpdateLayout();
        var boxes = Descendants(stack).OfType<KeyCaptureBox>().Where(box => box.Name.StartsWith("Mouse")).ToArray();
        Check(boxes.Length == 2 && boxes.All(box => !box.IsEnabled), "both actual XAML side-button key fields default disabled");
        vm.EnableMouse4AltClick = vm.EnableMouse5AltClick = true;
        PumpFor(10);
        Check(boxes.All(box => box.IsEnabled), "enabling side buttons enables key capture");
        Capture(boxes.Single(box => box.Name == "Mouse4AltKeyBox"), Key.Q);
        Capture(boxes.Single(box => box.Name == "Mouse5AltKeyBox"), Key.Z);
        vm.SaveCommand.Execute(null);
        PumpFor(10);
        Check(vm.Mouse4Key.VirtualKey == VirtualKeyCode.VK_Q && vm.Mouse5Key.VirtualKey == VirtualKeyCode.VK_Z,
            "actual XAML key capture persists through save");
        root.UpdateLayout();
        if (conflict)
        {
            vm.UseArrowKeys = true;
            vm.ToggleEntries[0].SelectedKey.VirtualKey = VirtualKeyCode.VK_W;
            vm.LeftKey.VirtualKey = VirtualKeyCode.VK_A;
            vm.Mouse4Key.VirtualKey = VirtualKeyCode.VK_S;
            vm.UseArrowKeys = false;
            Check(vm.InputBindingError.Contains("Skill Keys row 1: W"), "real layout displays specific skill conflict");
            root.Measure(new Size(566, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, 566, root.DesiredSize.Height));
            root.UpdateLayout();
        }
        var bitmap = new RenderTargetBitmap(566, (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(conflict ? "artifacts/input-bindings/settings-conflict-preview.png" : "artifacts/input-bindings/settings-preview.png"); encoder.Save(file);
        Console.WriteLine("PASS: actual settings XAML and styles rendered without starting the application.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }

    private static void SettingsChecks()
    {
        var defaults = Settings.Defaults.Clone();
        Check(!defaults.UseArrowKeys && !defaults.EnableMouse4AltClick && !defaults.EnableMouse5AltClick, "defaults preserve old inputs");
        var old = JsonSerializer.SerializeToNode(defaults)!.AsObject();
        foreach (var key in new[] { "UseArrowKeys", "Mouse4Key", "Mouse5Key", "EnableMouse4AltClick", "EnableMouse5AltClick" }) old.Remove(key);
        var loaded = old.Deserialize<Settings>()!;
        Check(!loaded.UseArrowKeys && !loaded.EnableMouse4AltClick && !loaded.EnableMouse5AltClick, "legacy JSON stays WASD and disabled");
        Check(loaded.Mouse4Key == VirtualKeyCode.NUMPAD4 && loaded.Mouse5Key == VirtualKeyCode.NUMPAD5, "missing key values get defaults");
        var store = new MemorySettings();
        var vm = new SettingsViewModel(store, new NoCursorFile());
        vm.UseArrowKeys = true;
        vm.EnableMouse4AltClick = vm.EnableMouse5AltClick = true;
        vm.Mouse4Key.VirtualKey = VirtualKeyCode.VK_Q; vm.Mouse5Key.VirtualKey = VirtualKeyCode.VK_Z;
        Check(vm.ValidateInputBindings() && vm.HasUnappliedChanges, "arrow and alt bindings valid without Z in Skill Keys");
        vm.ApplyCommand.Execute(null);
        Check(vm.UseArrowKeys && vm.EnableMouse4AltClick && vm.Mouse5Key.VirtualKey == VirtualKeyCode.VK_Z, "apply preserves fields");
        Check(!store.Saved.EnableMouse4AltClick, "apply does not persist");
        vm.SaveCommand.Execute(null);
        Check(store.Saved.UseArrowKeys && store.Saved.EnableMouse4AltClick && store.Saved.EnableMouse5AltClick
            && store.Saved.Mouse4Key == VirtualKeyCode.VK_Q && store.Saved.Mouse5Key == VirtualKeyCode.VK_Z, "save persists all options");
        Check(store.Saved.Equals(store.Saved.Clone()) && !store.Saved.Equals(defaults), "clone and equality include bindings");
        var roundTrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(store.Saved))!;
        Check(roundTrip.Equals(store.Saved), "JSON round trip");
        vm.ResetAllDefaultCommand.Execute(null);
        Check(!vm.UseArrowKeys && !vm.EnableMouse4AltClick && !vm.EnableMouse5AltClick, "reset defaults disables new options");
        vm.ResetAllCommand.Execute(null);
        Check(vm.UseArrowKeys && vm.EnableMouse4AltClick && vm.Mouse4Key.VirtualKey == VirtualKeyCode.VK_Q, "reset saved restores bindings");
        vm.Mouse5Key.VirtualKey = VirtualKeyCode.VK_Q;
        Check(vm.ValidateInputBindings(), "both side buttons may share an output");
        vm.Mouse4Key.VirtualKey = VirtualKeyCode.VK_Z;
        Check(vm.ValidateInputBindings(), "arbitrary keyboard target does not need a Skill Keys entry");
        vm.ValidateInputChange = () => "Turn movement off";
        vm.UseArrowKeys = false; vm.SaveCommand.Execute(null);
        Check(store.Saved.UseArrowKeys && vm.InputBindingError == "Turn movement off", "runtime guard blocks persistence");
        vm.ValidateInputChange = null; vm.UseArrowKeys = true;
        vm.ToggleEntries[0].SelectedKey.VirtualKey = VirtualKeyCode.UP;
        Check(!vm.ValidateInputBindings(), "arrow skill conflict rejected");
        vm.ToggleEntries[0].SelectedKey.VirtualKey = VirtualKeyCode.VK_Q;
        foreach (var forbidden in new[] { VirtualKeyCode.UP, vm.HoldToggleAltKey.VirtualKey, vm.LeftKey.VirtualKey,
            VirtualKeyCode.F13, VirtualKeyCode.ATTN, VirtualKeyCode.EXSEL, VirtualKeyCode.CRSEL, VirtualKeyCode.EREOF })
        {
            vm.Mouse4Key.VirtualKey = forbidden;
            Check(!vm.ValidateInputBindings(), "conflicting/internal output rejected: " + forbidden);
        }
        vm.EnableMouse4AltClick = false;
        Check(vm.ValidateInputBindings(), "disabled field cannot block valid configuration");
    }

    private static void LayoutConflictChecks()
    {
        var store = new MemorySettings();
        var vm = new SettingsViewModel(store, new NoCursorFile());
        int saved = 0, applied = 0, rejected = 0;
        vm.InputBindingsRejected += () => rejected++;
        vm.SaveRequested += () => saved++;
        vm.ApplyRequested += () => applied++;
        vm.UseArrowKeys = true;
        vm.ToggleEntries[0].SelectedKey.VirtualKey = VirtualKeyCode.VK_W;
        vm.ToggleEntries.Last().SelectedKey.VirtualKey = VirtualKeyCode.VK_D;
        vm.LeftKey.VirtualKey = VirtualKeyCode.VK_A;
        vm.EnableMouse4AltClick = true; vm.Mouse4Key.VirtualKey = VirtualKeyCode.VK_S;
        Check(vm.ValidateInputBindings(), "WASD bindings legal with arrow movement");
        vm.SaveCommand.Execute(null);
        var before = store.Saved.Clone();
        vm.UseArrowKeys = false;
        vm.SaveCommand.Execute(null); vm.ApplyCommand.Execute(null);
        Check(saved == 1 && applied == 0 && rejected == 2 && store.Saved.Equals(before), "conflicting layout switch cannot save or apply and preserves saved bindings");
        Check(vm.InputBindingError.Contains("WASD") && vm.InputBindingError.Contains("W")
            && vm.InputBindingError.Contains("Left Alt Click") && vm.InputBindingError.Contains("Mouse 4"), "layout error identifies conflicting keys and fields");
        Check(vm.ToggleEntries[0].SelectedKey.VirtualKey == VirtualKeyCode.VK_W && vm.LeftKey.VirtualKey == VirtualKeyCode.VK_A,
            "rejected layout switch preserves editable bindings");
        vm.UseArrowKeys = true;
        Check(vm.InputBindingError == "", "switching back clears warning immediately");
        vm.UseArrowKeys = false;
        Check(vm.InputBindingError.Contains("WASD"), "layout switch warns immediately before save");
        vm.ToggleEntries[0].SelectedKey.VirtualKey = VirtualKeyCode.VK_Q;
        vm.ToggleEntries.Last().SelectedKey.VirtualKey = VirtualKeyCode.VK_F;
        vm.LeftKey.VirtualKey = VirtualKeyCode.NUMPAD8;
        vm.EnableMouse4AltClick = false;
        Check(vm.InputBindingError == "", "fixing all active conflicts clears warning immediately");
        vm.SaveCommand.Execute(null);
        Check(saved == 2 && !store.Saved.UseArrowKeys && store.Saved.Mouse4Key == VirtualKeyCode.VK_S,
            "resolved layout saves while preserving disabled side target");
        vm.EnableMouse4AltClick = true;
        Check(vm.InputBindingError.Contains("Mouse 4"), "reenabling conflicting side mapping immediately warns");
        vm.ResetAllCommand.Execute(null);
        Check(!vm.EnableMouse4AltClick && vm.InputBindingError == "", "reset restores valid saved state");

        foreach (bool arrows in new[] { false, true })
        {
            foreach (var binding in MovementBindings.ForLayout(arrows))
            {
                var physical = Helper.ToWinFormsKey(binding.Physical);
                // Every old/new key field must reject keys used by the selected layout.
                for (int field = 0; field < 18; field++)
                {
                    var sample = new SettingsViewModel(new MemorySettings(), new NoCursorFile()) { UseArrowKeys = arrows };
                    var fields = new[] { sample.MovementKey, sample.StandKey, sample.ToggleOverlayKey, sample.ToggleVisualCursorKey,
                        sample.HoldToggleVisualCursorKey, sample.HoldToggleAltKey, sample.SetMidpointKey, sample.TeleportMidpointKey,
                        sample.ApplyKey, sample.CenterOverlayKey, sample.EnableSetMidpointKey, sample.LeftKey, sample.RightKey, sample.MiddleKey,
                        sample.ToggleEntries[0].SelectedKey, sample.ToggleEntries.Last().SelectedKey, sample.Mouse4Key, sample.Mouse5Key };
                    if (field == 16) sample.EnableMouse4AltClick = true;
                    if (field == 17) sample.EnableMouse5AltClick = true;
                    fields[field].VirtualKey = physical;
                    Check(!sample.ValidateInputBindings(), $"{binding.Physical} rejected in field {field} with arrows={arrows}");
                }
            }
        }

        var reverse = new SettingsViewModel(new MemorySettings(), new NoCursorFile());
        reverse.ToggleEntries[0].SelectedKey.VirtualKey = VirtualKeyCode.UP;
        reverse.RightKey.VirtualKey = VirtualKeyCode.LEFT;
        Check(reverse.ValidateInputBindings(), "arrow skills and clicks legal with WASD movement");
        reverse.UseArrowKeys = true;
        Check(reverse.InputBindingError.Contains("Up") && reverse.InputBindingError.Contains("Right Alt Click"), "reverse layout switch reports arrow conflicts");
        reverse.ToggleEntries.RemoveAt(0); reverse.RightKey.VirtualKey = VirtualKeyCode.NUMPAD9;
        Check(reverse.InputBindingError == "", "removing conflicting skill and remapping click clears warning");

        var legacy = new MemorySettings();
        legacy.Saved.KeysToMapToToggleEntries = new[] { VirtualKeyCode.VK_W };
        var loaded = new SettingsViewModel(legacy, new NoCursorFile());
        Check(loaded.InputBindingError.Contains("WASD") && legacy.Saved.KeysToMapToToggleEntries[0] == VirtualKeyCode.VK_W,
            "legacy conflicting settings load with actionable warning and no data loss");
        var service = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(OverlayService));
        var update = typeof(OverlayService).GetMethod("UpdateManagers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(Equals(update.Invoke(service, new object[] { loaded }), false),
            "invalid configuration rejected before runtime managers are accessed or partially changed");
    }

    private static void MovementChecks()
    {
        var wasd = MovementBindings.ForLayout(false).ToArray();
        var arrows = MovementBindings.ForLayout(true).ToArray();
        Check(wasd.Select(b => b.Physical).SequenceEqual(new[] { Key.W, Key.A, Key.S, Key.D }), "default WASD order preserved");
        Check(arrows.Select(b => b.Physical).SequenceEqual(new[] { Key.Up, Key.Left, Key.Down, Key.Right }), "arrow layout");
        Check(wasd.Select(b => b.Logical).SequenceEqual(arrows.Select(b => b.Logical)), "controller directions identical");
        for (int mask = 0; mask < 16; mask++)
        {
            var wHeld = wasd.Where((_, index) => (mask & (1 << index)) != 0).Select(b => b.Physical).ToHashSet();
            var aHeld = arrows.Where((_, index) => (mask & (1 << index)) != 0).Select(b => b.Physical).ToHashSet();
            Check(MovementBindings.AnyHeld(false, wHeld.Contains) == MovementBindings.AnyHeld(true, aHeld.Contains), "held-key equivalence including opposing directions");
        }
        Check(!MovementBindings.AnyHeld(true, key => key == Key.W), "physical W no longer satisfies arrow movement guard");
    }

    private static void Capture(KeyCaptureBox box, Key key)
    {
        var evt = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(), Environment.TickCount, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        box.RaiseEvent(evt);
        Check(evt.Handled && box.SelectedKey == key, "real key-capture handler accepts " + key);
    }

    private static void CaptureChecks()
    {
        var store = new MemorySettings();
        var vm = new SettingsViewModel(store, new NoCursorFile());
        var box = new KeyCaptureBox { DataContext = vm };
        box.SetBinding(KeyCaptureBox.SelectedKeyProperty, new Binding("Mouse4Key.DisplayKey") { Mode = BindingMode.TwoWay });
        vm.EnableMouse4AltClick = true;
        Capture(box, Key.Z);
        Check(vm.Mouse4Key.VirtualKey == VirtualKeyCode.VK_Z && vm.HasUnappliedChanges, "captured key updates model and dirty state");
        vm.SaveCommand.Execute(null); PumpFor(10);
        Check(store.Saved.Mouse4Key == VirtualKeyCode.VK_Z && box.SelectedKey == Key.Z, "captured key survives save");
        Capture(box, Key.Q);
        vm.ResetAllCommand.Execute(null); PumpFor(10);
        Check(box.SelectedKey == Key.Z, "reset saved updates key box");
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void MouseHookChecks()
    {
        var seen = new List<(int Button, bool Down)>();
        var mapper = new MouseClickKeyMapper(new Dictionary<System.Windows.Forms.MouseButtons, VirtualKeyCode>())
        { SkipLogic = true, SideButtonChanged = (button, down) => { seen.Add((button, down)); return true; } };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.MSLLHOOKSTRUCT>());
        try
        {
            foreach (int number in new[] { 1, 2 })
            foreach (bool down in new[] { true, false })
            {
                Marshal.StructureToPtr(new Win32.MSLLHOOKSTRUCT { mouseData = (uint)number << 16 }, memory, false);
                var result = typeof(MouseClickKeyMapper).GetMethod("HookCallback", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(mapper, new object[] { 0, new IntPtr(down ? Win32.WM_XBUTTONDOWN : Win32.WM_XBUTTONUP), memory });
                Check((IntPtr)result! == new IntPtr(1) && seen.Last() == (number + 3, down), "native side event decoded and captured even for matching up during skip");
            }
            int before = seen.Count;
            foreach (uint flags in new[] { Win32.LLMHF_INJECTED, Win32.LLMHF_LOWER_IL_INJECTED })
            {
                Marshal.StructureToPtr(new Win32.MSLLHOOKSTRUCT { mouseData = 1u << 16, flags = flags }, memory, false);
                typeof(MouseClickKeyMapper).GetMethod("HookCallback", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(mapper, new object[] { 0, new IntPtr(Win32.WM_XBUTTONDOWN), memory });
            }
            Check(seen.Count == before, "injected side mouse output cannot recurse into input routing");
        }
        finally { Marshal.FreeHGlobal(memory); mapper.Dispose(); }
    }

    private static void RoutingChecks()
    {
        var vm = new SettingsViewModel(new MemorySettings(), new NoCursorFile());
        var processor = new Recorder();
        var state = new IdleState();
        var controller = new ControllerManager(new Lazy<IEventProcessor>(() => processor), new KeyStateTracker(), null!, state, null!);
        controller.DirectionalKeys = new List<Key> { Key.F17 };
        controller.ToggleKeys = new List<Key> { Key.F13, Key.CrSel, Key.EraseEof };
        var output = new List<(VirtualKeyCode Key, uint Flags, ulong Tag)>();
        var mouse = new List<(int Button, bool Down)>();
        bool modifier = false;
        HotkeyController? hotkeys = null;
        hotkeys = new HotkeyController(new Window(), vm, controller, (key, flags, tag) =>
        {
            output.Add((key, flags, tag));
            if (tag == Win32.PlaceholderInjectionTag)
            {
                var callbacks = Field<Dictionary<VirtualKeyCode, Action>>(hotkeys!, flags == 0 ? "_skillDownCallbacks" : "_releaseCallbacks");
                if (callbacks.TryGetValue(key, out var callback)) callback();
            }
        }, (button, down) => mouse.Add((button, down)), _ => modifier);
        Invoke(hotkeys, "BindToggleEntries");
        hotkeys.SideButtonsEnabled = true;
        Check(!hotkeys.HandleSideButton(4, true) && !hotkeys.HandleSideButton(4, false), "disabled button passes through");
        hotkeys.Mouse4Key = VirtualKeyCode.VK_Q; hotkeys.Mouse5Key = VirtualKeyCode.VK_Z;
        Invoke(hotkeys, "BindToggleEntries");

        foreach (int button in new[] { 4, 5 })
        foreach (bool reverse in new[] { false, true })
        foreach (bool held in new[] { false, true })
        {
            output.Clear(); mouse.Clear(); processor.Events.Clear();
            vm.InvertAltMode = reverse; modifier = held;
            bool raw = reverse ? held : !held;
            var target = button == 4 ? VirtualKeyCode.VK_Q : VirtualKeyCode.VK_Z;
            Check(hotkeys.HandleSideButton(button, true), "side down captured");
            PumpUntil(() => raw ? mouse.Any(e => e.Down) : output.Any(e => e.Key == target && e.Flags == 0));
            Check(raw ? output.Count == 0 && mouse.Single() == (button, true) : mouse.Count == 0, "modifier/reverse selects correct output");
            Check(processor.Events.Any(e => e.Key == (button == 4 ? Key.CrSel : Key.EraseEof) && e.Action == KeyAction.Down), "side uses its own aimed controller slot");
            modifier = !held; // Release must follow the captured press, not the new modifier state.
            Check(hotkeys.HandleSideButton(button, false), "side up captured");
            PumpUntil(() => raw ? mouse.Any(e => !e.Down) : output.Any(e => e.Key == target && e.Flags == Win32.KEYEVENTF_KEYUP));
            Check(raw ? mouse.SequenceEqual(new[] { (button, true), (button, false) })
                : output.Count(e => e.Key == target && e.Flags == 0) == 1 && output.Count(e => e.Key == target && e.Flags == Win32.KEYEVENTF_KEYUP) == 1,
                "modifier change during hold still produces balanced output");
        }

        vm.InvertAltMode = false; modifier = true;
        hotkeys.Mouse5Key = VirtualKeyCode.VK_Q;
        output.Clear(); mouse.Clear();
        hotkeys.HandleSideButton(4, true);
        PumpUntil(() => output.Any(e => e.Flags == 0));
        hotkeys.HandleSideButton(5, true); PumpFor(60);
        SendPhysical(hotkeys, VirtualKeyCode.VK_Q, true); PumpFor(60);
        hotkeys.HandleSideButton(4, false); hotkeys.HandleSideButton(5, false); PumpFor(80);
        Check(!output.Any(e => e.Key == VirtualKeyCode.VK_Q && e.Flags != 0), "keyboard keeps shared output held after both side releases");
        SendPhysical(hotkeys, VirtualKeyCode.VK_Q, false);
        PumpUntil(() => output.Any(e => e.Key == VirtualKeyCode.VK_Q && e.Flags != 0));
        Check(output.Count(e => e.Key == VirtualKeyCode.VK_Q && e.Flags == 0 && e.Tag == Win32.LiteralInjectionTag) == 1,
            "shared sources emit exactly one key down");

        // A new physical press must survive an older press's delayed controller release.
        output.Clear();
        hotkeys.HandleSideButton(4, true); PumpUntil(() => output.Count > 0);
        processor.HoldReleases = true;
        hotkeys.HandleSideButton(4, false); PumpUntil(() => !processor.PendingReleases.IsEmpty);
        hotkeys.HandleSideButton(4, true); PumpFor(60);
        while (processor.PendingReleases.TryDequeue(out var pending)) pending.Tcs.TrySetResult(null!);
        PumpFor(30);
        Check(!output.Any(e => e.Flags != 0), "older release cannot release a newer held side press");
        processor.HoldReleases = false;
        hotkeys.HandleSideButton(4, false); PumpUntil(() => output.Any(e => e.Flags != 0));

        output.Clear();
        hotkeys.HandleSideButton(4, true);
        hotkeys.SideButtonsEnabled = false; hotkeys.ReleaseActiveInputs(); PumpFor(100);
        Check(!output.Any(e => e.Flags == 0), "mode-off cancels delayed output down");
        Check(hotkeys.HandleSideButton(4, false) && !hotkeys.HandleSideButton(4, true), "matching up consumed but new presses pass while off");
        Check(!hotkeys.HandleSideButton(6, true), "unsupported button ignored");
        hotkeys.SideButtonsEnabled = true;
        foreach (bool raw in new[] { false, true })
        {
            modifier = !raw; output.Clear(); mouse.Clear();
            hotkeys.HandleSideButton(5, true);
            PumpUntil(() => raw ? mouse.Count > 0 : output.Count > 0);
            processor.HoldReleases = true;
            hotkeys.HandleSideButton(5, false); PumpUntil(() => !processor.PendingReleases.IsEmpty);
            hotkeys.ReleaseActiveInputs();
            Check(raw ? mouse.Last() == (5, false) : output.Last().Flags == Win32.KEYEVENTF_KEYUP, "cleanup releases held mouse/key output despite pending controller");
            int count = output.Count + mouse.Count;
            while (processor.PendingReleases.TryDequeue(out var pending)) pending.Tcs.TrySetResult(null!);
            PumpFor(30);
            Check(output.Count + mouse.Count == count, "stale controller completion cannot emit output");
            processor.HoldReleases = false;
        }

        // Actual directional suspend/restore branches with a recorded controller.
        modifier = true; output.Clear();
        state.HeldSkills = new List<Key> { Key.CrSel }; state.MovementHeld = true;
        hotkeys.HandleSideButton(4, true); PumpUntil(() => output.Count > 0);
        SendPhysical(hotkeys, VirtualKeyCode.VK_F, true);
        PumpUntil(() => output.Any(e => e.Key == VirtualKeyCode.VK_F && e.Tag == Win32.LiteralInjectionTag && e.Flags == 0));
        Check(output.Any(e => e.Key == VirtualKeyCode.VK_Q && e.Tag == Win32.DirectionalRestoreInjectionTag && e.Flags != 0), "directional skill suspends side output");
        SendPhysical(hotkeys, VirtualKeyCode.VK_F, false);
        PumpUntil(() => output.Any(e => e.Key == VirtualKeyCode.VK_Q && e.Tag == Win32.DirectionalRestoreInjectionTag && e.Flags == 0));
        Check(true, "directional release restores held side output");
        hotkeys.HandleSideButton(4, false); PumpFor(60);
        output.Clear();
        hotkeys.HandleSideButton(4, true); PumpUntil(() => output.Count > 0);
        SendPhysical(hotkeys, VirtualKeyCode.VK_F, true);
        PumpUntil(() => output.Any(e => e.Key == VirtualKeyCode.VK_F && e.Tag == Win32.LiteralInjectionTag && e.Flags == 0));
        SendPhysical(hotkeys, VirtualKeyCode.VK_F, false);
        hotkeys.HandleSideButton(4, false); PumpFor(120);
        Check(!output.Any(e => e.Key == VirtualKeyCode.VK_Q && e.Tag == Win32.DirectionalRestoreInjectionTag && e.Flags == 0), "released side button cannot be restored by directional release");
        state.MovementHeld = false; state.HeldSkills.Clear();

        controller.UseArrowKeys = true;
        Invoke(hotkeys, "BindToggleEntries");
        var downs = Field<Dictionary<VirtualKeyCode, Action>>(hotkeys, "_skillDownCallbacks");
        var ups = Field<Dictionary<VirtualKeyCode, Action>>(hotkeys, "_releaseCallbacks");
        var swaps = Field<Dictionary<VirtualKeyCode, VirtualKeyCode>>(hotkeys, "_swapMap");
        Check(swaps[VirtualKeyCode.F23] == Helper.ToWinFormsKey(Key.NoName)
            && swaps[VirtualKeyCode.F24] == Helper.ToWinFormsKey(Key.Pa1)
            && swaps[VirtualKeyCode.F22] == Helper.ToWinFormsKey(Key.Oem102), "existing left/right/middle routing retained");
        Check(!downs.ContainsKey(VirtualKeyCode.VK_W) && downs.ContainsKey(VirtualKeyCode.UP), "movement callbacks use arrows only");
        var before = processor.Events.Count;
        downs[VirtualKeyCode.UP](); PumpUntil(() => processor.Events.Count > before);
        Check(processor.Events.Last().Key == Key.W && processor.Events.Last().Action == KeyAction.Down, "arrow down delivers logical W");
        before = processor.Events.Count;
        ups[VirtualKeyCode.UP](); PumpUntil(() => processor.Events.Count > before);
        Check(processor.Events.Last().Key == Key.W && processor.Events.Last().Action == KeyAction.Up, "arrow release delivers logical W up");
        hotkeys.ReleaseActiveInputs();
    }

    private static void SendPhysical(HotkeyController hotkeys, VirtualKeyCode key, bool down)
    {
        var info = new Win32.KBDLLHOOKSTRUCT { vkCode = (uint)key };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());
        try
        {
            Marshal.StructureToPtr(info, memory, false);
            var result = typeof(HotkeyController).GetMethod("HookCallback", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(hotkeys, new object[] { 0, new IntPtr(down ? 0x100 : 0x101), memory });
            Check((IntPtr)result! == new IntPtr(1), "mapped physical key consumed");
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, null);
    private static void PumpUntil(Func<bool> complete)
    {
        var watch = Stopwatch.StartNew();
        while (!complete())
        {
            if (watch.ElapsedMilliseconds > 3000) throw new TimeoutException("Input pipeline did not complete");
            PumpFor(5);
        }
    }
    private static void PumpFor(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    private sealed class Recorder : IEventProcessor
    {
        public ConcurrentQueue<KeyEvent> Events = new();
        public ConcurrentQueue<KeyEvent> PendingReleases = new();
        public volatile bool HoldReleases;
        public async Task ProcessLoopAsync(System.Threading.Channels.ChannelReader<KeyEvent> reader, IKeyStateTracker tracker)
        {
            await foreach (var evt in reader.ReadAllAsync())
            {
                Events.Enqueue(evt);
                if (HoldReleases && evt.Action == KeyAction.Up) PendingReleases.Enqueue(evt);
                else evt.Tcs.TrySetResult(null!);
            }
        }
    }
    private sealed class IdleState : IControllerState
    {
        public bool IncomingSkill => false; public bool IncomingDirectionalSkill => false; public bool IncomingWASD => false;
        public bool AnySkillDown => false; public bool AnyDirectionalSkillDown => false;
        public List<Key> CurrentlyHeldSkillKeys => new(); public List<Key> CurrentlyHeldWASDKeys => new();
        public bool AnyOtherSkillIsCurrentlyHeldDown => false; public bool AnyOtherDirectionalSkillIsCurrentlyHeldDown => false;
        public bool MovementHeld; public List<Key> HeldSkills = new();
        public bool AnyOtherWASDIsCurrentlyHeldDown => MovementHeld; public List<Key> AllHeldSkillDown(List<Key> keys) => HeldSkills.ToList();
        public VirtualKeyCode StandKey { get; set; } public VirtualKeyCode MovementKey { get; set; }
        public Task StandInPlace() => Task.CompletedTask; public Task DontStandInPlace() => Task.CompletedTask;
        public Task MovePlace() => Task.CompletedTask; public Task DontMovePlace() => Task.CompletedTask;
    }
    private sealed class MemorySettings : ISettingService
    {
        public Settings Saved = Settings.Defaults.Clone();
        public Settings Load() => Saved.Clone();
        public void Save(Settings settings) => Saved = settings.Clone();
    }
    private sealed class NoCursorFile : ICursorImageLoader
    {
        public string GetCursorFilePath() => "";
        public void SaveFromFile(string path) => throw new NotSupportedException();
    }
}
