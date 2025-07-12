using System.Windows.Input;
using PathOfWASD.Managers.Controller.Interfaces;

namespace PathOfWASD.Managers.Controller;

public class SkillUpDelayHandler : ISkillUpDelayHandler
{
    private readonly Lazy<ControllerManager> _controllerManager;
    public TimeSpan SkillHoldThreshold { get; } = TimeSpan.FromMilliseconds(200);
    
    public SkillUpDelayHandler(Lazy<ControllerManager> controllerManager)
    {
        _controllerManager = controllerManager;
    }

    public async Task DelaySkillUpAsync(KeyEvent evt, CancellationTokenSource cts, IKeyStateTracker tracker)
    {
        try
        {
            var downTime = _controllerManager.Value.SkillDownTimes.TryGetValue(evt.Key, out var start)
                ? start
                : DateTime.UtcNow;
            var held = DateTime.UtcNow - downTime;
            var remaining = SkillHoldThreshold - held;

            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, cts.Token);

            tracker.OnUp(evt.Key);
            await _controllerManager.Value.ProcessEventAsync(evt);
            evt.Tcs.SetResult(null);
        }
        catch (TaskCanceledException) { }
        finally
        {
            _controllerManager.Value.PendingSkillUpCts.Remove(evt.Key);
            _controllerManager.Value.SkillDownTimes.Remove(evt.Key);
        }
    }
}