using System.Threading.Channels;
using PathOfWASD.Managers.Controller.Interfaces;

namespace PathOfWASD.Managers.Controller;

public class EventProcessor : IEventProcessor
{
    private readonly ISkillUpDelayHandler _skillUpDelayHandler;
    private readonly Lazy<ControllerManager> _controllerManager;

    public EventProcessor(ISkillUpDelayHandler skillUpDelayHandler, Lazy<ControllerManager> controllerManager)
    {
        _skillUpDelayHandler = skillUpDelayHandler;
        _controllerManager = controllerManager;
    }

    public async Task ProcessLoopAsync(ChannelReader<KeyEvent> reader, IKeyStateTracker tracker)
    {
        await foreach (var evt in reader.ReadAllAsync())
        {
            try
            {
                if (evt.Action == KeyAction.Down)
                {
                    if (_controllerManager.Value.PendingSkillUpCts.TryGetValue(evt.Key, out var cts))
                    {
                        cts.Cancel();
                        _controllerManager.Value.PendingSkillUpCts.Remove(evt.Key);
                    }

                    if (tracker.IsHeld(evt.Key))
                    {
                        evt.Tcs.SetResult(null);
                        continue;
                    }

                    tracker.OnDown(evt.Key);
                    await _controllerManager.Value.ProcessEventAsync(evt);
                    evt.Tcs.SetResult(null);
                }
                else
                {
                    if (!tracker.IsHeld(evt.Key))
                    {
                        evt.Tcs.SetResult(null);
                        continue;
                    }

                    if (_controllerManager.Value.ToggleKeys.Contains(evt.Key))
                    {
                        var ctsUp = new CancellationTokenSource();
                        _controllerManager.Value.PendingSkillUpCts[evt.Key] = ctsUp;
                        _ = _skillUpDelayHandler.DelaySkillUpAsync(evt, ctsUp, tracker);
                    }
                    else
                    {
                        tracker.OnUp(evt.Key);
                        await _controllerManager.Value.ProcessEventAsync(evt);
                        evt.Tcs.SetResult(null);
                    }
                }
            }
            catch (Exception ex)
            {
                evt.Tcs.SetException(ex);
            }
        }
    }
}