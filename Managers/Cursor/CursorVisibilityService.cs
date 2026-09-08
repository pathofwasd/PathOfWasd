using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using PathOfWASD.Internals;

namespace PathOfWASD.Managers.Cursor;

/// <summary>Opt-in cursor visibility; never changes game input or overlay hit testing.</summary>
public sealed class CursorVisibilityService : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private Process? _watchdog;
    private volatile bool _enabled, _requested;
    private bool _wasdActive, _initialized, _hidden, _faulted;
    public event Action<string>? StatusChanged;

    public CursorVisibilityService() => _timer.Tick += (_, _) => Refresh();

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;
        if (!enabled)
        {
            Stop();
            _faulted = false;
            StatusChanged?.Invoke("");
            return;
        }
        try
        {
            if (!Environment.Is64BitProcess || !MagnificationCursor.MagInitialize())
                throw new InvalidOperationException("Windows cursor hiding could not initialize.");
            _initialized = true;
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true
            };
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, Assembly.GetEntryAssembly()!.GetName().Name + ".dll"));
            start.ArgumentList.Add(CursorVisibilityWatchdog.Argument);
            _watchdog = Process.Start(start) ?? throw new IOException("Cursor recovery helper did not start.");
            _watchdog.StandardInput.AutoFlush = true;
            var ready = _watchdog.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            if (ready != "ready") throw new IOException("Cursor recovery helper is unavailable.");
            _timer.Start();
            StatusChanged?.Invoke("Experimental: tested in Path of Exile; limited testing.");
            Refresh();
        }
        catch (Exception ex) { Fail(ex); }
    }

    public void RequestHidden(bool requested)
    {
        if (!_enabled) { _requested = requested; return; }
        if (!_timer.Dispatcher.CheckAccess())
        {
            if (!_timer.Dispatcher.HasShutdownStarted)
                _timer.Dispatcher.Invoke(() => RequestHidden(requested));
            return;
        }
        _requested = requested;
        Refresh();
    }

    public void SetWasdActive(bool active)
    {
        _wasdActive = active;
        Refresh();
    }

    private void Refresh()
    {
        if (!_enabled || _faulted || !_initialized) return;
        try
        {
            if (_watchdog == null || _watchdog.HasExited)
                throw new IOException("Cursor recovery helper stopped.");
            bool hide = _wasdActive && _requested && MagnificationCursor.IsPoeForeground();
            if (hide && !_hidden)
            {
                // Arm recovery BEFORE attempting to change desktop visibility.
                _watchdog.StandardInput.WriteLine("hidden");
                _hidden = true;
                if (!MagnificationCursor.MagShowSystemCursor(false))
                    throw new InvalidOperationException("Windows refused to hide the cursor.");
            }
            else if (!hide && _hidden)
            {
                if (!MagnificationCursor.MagShowSystemCursor(true))
                    throw new InvalidOperationException("Windows refused to restore the cursor.");
                _hidden = false;
            }
            _watchdog.StandardInput.WriteLine(_hidden ? "hidden" : "visible");
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
        _faulted = true; // Explicitly switch the setting off/on to retry.
        Trace.TraceError($"Cursor hiding disabled: {ex.Message}");
        Stop();
        StatusChanged?.Invoke("Cursor hiding unavailable. Switch this option off and on to retry.");
    }

    private void Stop()
    {
        _timer.Stop();
        try
        {
            if (_hidden && _initialized && MagnificationCursor.MagShowSystemCursor(true))
            {
                _hidden = false;
                _watchdog?.StandardInput.WriteLine("visible");
            }
        }
        catch (Exception ex) { Trace.TraceError(ex.Message); }
        finally
        {
            try { _watchdog?.StandardInput.Dispose(); }
            catch (Exception ex) { Trace.TraceError(ex.Message); }
            _watchdog?.Dispose(); // EOF asks the helper to restore; do not kill it.
            _watchdog = null;
            if (_initialized) MagnificationCursor.MagUninitialize();
            _initialized = false;
        }
    }

    public void Dispose()
    {
        _enabled = false;
        Stop();
    }
}
