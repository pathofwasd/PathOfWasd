extern alias product;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using PathOfWASD.Internals;
using PathOfWASD.Managers.Cursor;
using Settings = product::PathOfWASD.Overlays.Settings.ViewModels.Settings;
using SettingsViewModel = product::PathOfWASD.Overlays.Settings.ViewModels.SettingsViewModel;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args is [CursorVisibilityWatchdog.Argument]) { CursorVisibilityWatchdog.Run(); return; }
        SettingsRoundTrip();
        ServiceTransitions();
        ServiceFailures();
        WatchdogRecovery();
        Console.WriteLine($"PASS: {_checks} cursor visibility checks. Native cursor calls were faked.");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }

    private static void SettingsRoundTrip()
    {
        var defaults = Settings.Defaults.Clone();
        Check(!defaults.HideMovementCursor, "defaults stay off");
        var oldJson = JsonSerializer.SerializeToNode(defaults)!.AsObject();
        oldJson.Remove(nameof(Settings.HideMovementCursor));
        Check(!oldJson.Deserialize<Settings>()!.HideMovementCursor, "old settings stay off despite legacy AlwaysHide");
        defaults.HideMovementCursor = true;
        Check(defaults.Clone().HideMovementCursor && defaults.Equals(defaults.Clone()), "clone preserves option");
        Check(!defaults.Equals(Settings.Defaults), "dirty comparison detects option");
        Check(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(defaults))!.HideMovementCursor, "JSON preserves option");

        var store = new MemorySettings();
        var vm = new SettingsViewModel(store, new NoCursorFile());
        vm.HideMovementCursor = true;
        Check(vm.HasUnappliedChanges, "view model becomes dirty");
        vm.ApplyCommand.Execute(null);
        Check(vm.HideMovementCursor && !vm.HasUnappliedChanges && !store.Saved.HideMovementCursor, "apply retains option without saving");
        vm.SaveCommand.Execute(null);
        Check(store.Saved.HideMovementCursor, "save stores option");
        vm.HideMovementCursor = false;
        vm.ResetAllCommand.Execute(null);
        Check(vm.HideMovementCursor, "reset to saved restores option");
        vm.ResetAllDefaultCommand.Execute(null);
        Check(!vm.HideMovementCursor, "reset defaults disables option");
    }

    private static void ServiceTransitions()
    {
        MagnificationCursor.Reset();
        using var service = new CursorVisibilityService();
        service.RequestHidden(true);
        service.SetWasdActive(true);
        service.SetEnabled(false);
        Check(MagnificationCursor.Initializes == 0 && MagnificationCursor.Hides == 0 && MagnificationCursor.Shows == 0,
            "disabled path makes no native calls");
        service.SetWasdActive(false);
        service.SetEnabled(true);
        Check(MagnificationCursor.Initializes == 1 && MagnificationCursor.Hides == 0, "enabling outside WASD does not hide");
        service.SetWasdActive(true);
        Check(MagnificationCursor.Hides == 1, "WASD lock hides");
        service.RequestHidden(true);
        Check(MagnificationCursor.Hides == 1, "repeated lock does not repeat native call");
        var uiThread = Environment.CurrentManagedThreadId;
        var worker = Task.Run(() => service.RequestHidden(false));
        var frame = new DispatcherFrame();
        var limit = Stopwatch.StartNew();
        var pump = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        pump.Tick += (_, _) => { if (worker.IsCompleted || limit.ElapsedMilliseconds > 3000) frame.Continue = false; };
        pump.Start();
        Dispatcher.PushFrame(frame);
        pump.Stop();
        Check(worker.IsCompletedSuccessfully, "controller thread can request visibility on the UI dispatcher");
        Check(MagnificationCursor.Shows == 1, "skill unlock restores synchronously");
        Check(MagnificationCursor.LastVisibilityThread == uiThread, "native visibility stays on the initialization thread");
        service.RequestHidden(true);
        MagnificationCursor.Foreground = false;
        service.RequestHidden(true);
        Check(MagnificationCursor.Shows == 2, "focus loss restores");
        MagnificationCursor.Foreground = true;
        service.RequestHidden(true);
        Check(MagnificationCursor.Hides == 3, "focus return permits hiding");
        service.SetWasdActive(false);
        service.RequestHidden(true);
        Check(MagnificationCursor.Hides == 3 && MagnificationCursor.Shows == 3, "late skill release cannot hide outside WASD");
        service.SetWasdActive(true);
        service.SetEnabled(false);
        Check(MagnificationCursor.Shows == 4, "disable restores");
        service.Dispose();
        Check(MagnificationCursor.Shows == 4, "dispose does not change unrelated cursor state");
    }

    private static void ServiceFailures()
    {
        MagnificationCursor.Reset();
        MagnificationCursor.InitializeSucceeds = false;
        using (var service = new CursorVisibilityService())
        {
            string status = "";
            service.StatusChanged += text => status = text;
            service.SetEnabled(true);
            service.SetWasdActive(true);
            service.RequestHidden(true);
            Check(status.Contains("unavailable") && MagnificationCursor.Hides == 0, "init failure leaves cursor alone");
        }
        MagnificationCursor.Reset();
        using (var service = new CursorVisibilityService())
        {
            service.SetEnabled(true);
            service.SetWasdActive(true);
            MagnificationCursor.HideSucceeds = false;
            service.RequestHidden(true);
            Check(MagnificationCursor.Hides == 1 && MagnificationCursor.Shows == 1, "failed hide attempts restoration");
            service.RequestHidden(true);
            Check(MagnificationCursor.Hides == 1, "failure latches instead of repeatedly hiding");
        }
        MagnificationCursor.Reset();
        using (var service = new CursorVisibilityService())
        {
            service.SetEnabled(true);
            service.SetWasdActive(true);
            service.RequestHidden(true);
            var child = (Process)typeof(CursorVisibilityService).GetField("_watchdog", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service)!;
            child.Kill(); // Only this test's own helper; never a game process.
            child.WaitForExit();
            service.RequestHidden(true);
            Check(MagnificationCursor.Shows == 1, "helper death restores from main process");
        }
    }

    private static void WatchdogRecovery()
    {
        foreach (var (commands, expected) in new[] { ("", 0), ("visible\n", 0), ("hidden\n", 1), ("hidden\nvisible\n", 0), ("hidden\nbad\n", 1) })
        {
            int restores = 0;
            CursorVisibilityWatchdog.Monitor(new StringReader(commands), new StringWriter(), TimeSpan.FromSeconds(1), () => restores++);
            Check(restores == expected, $"helper EOF/protocol recovery: {commands.Trim()}");
        }
        int timedOut = 0;
        CursorVisibilityWatchdog.Monitor(new StalledReader(), new StringWriter(), TimeSpan.FromMilliseconds(50), () => timedOut++);
        Check(timedOut == 1, "UI heartbeat timeout restores");
    }

    private sealed class StalledReader : TextReader
    {
        private bool _armed;
        public override string? ReadLine()
        {
            if (!_armed) { _armed = true; return "hidden"; }
            Thread.Sleep(200);
            return null;
        }
    }

    private sealed class MemorySettings : product::PathOfWASD.Overlays.Settings.ViewModels.ISettingService
    {
        public Settings Saved = Settings.Defaults.Clone();
        public Settings Load() => Saved.Clone();
        public void Save(Settings settings) => Saved = settings.Clone();
    }

    private sealed class NoCursorFile : product::PathOfWASD.Overlays.Cursor.Interfaces.ICursorImageLoader
    {
        public string GetCursorFilePath() => "";
        public void SaveFromFile(string path) => throw new NotSupportedException();
    }
}
