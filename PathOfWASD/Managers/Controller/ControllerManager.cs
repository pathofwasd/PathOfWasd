using System.Threading.Channels;
using System.Windows.Input;
using PathOfWASD.Managers.Controller.Interfaces;
using PathOfWASD.Managers.Cursor;
using MouseButton = WindowsInput.MouseButton;

namespace PathOfWASD.Managers.Controller
{
    public enum KeyAction { Down, Up }

    public record KeyEvent(KeyAction Action, Key Key, TaskCompletionSource<object> Tcs);

    public class ControllerManager
    {
        private readonly Channel<KeyEvent> _channel;
        private readonly IKeyStateTracker _tracker;
        private readonly CursorManager _cursorManager;
        private readonly Lazy<IEventProcessor> _eventProcessor;
        private readonly DelayMovementUpState _delayMovementUpState;
        
        public IControllerState State { get; }
        
        private bool _altMode;

        public List<Key> ToggleKeys { get; set; }
        public List<Key> DirectionalKeys { get; set; }
        public Key CurrentKey { get; private set; }
        public bool IsClick { get; set; }

        public Dictionary<Key, CancellationTokenSource> PendingSkillUpCts { get; } = new();
        public Dictionary<Key, DateTime> SkillDownTimes { get; } = new();
        
        public void ClearStates()
        {
            _tracker.Clear();
            PendingSkillUpCts.Clear();
            SkillDownTimes.Clear();
            _delayMovementUpState.MovementStarted = false;
        }
        
        public ControllerManager(Lazy<IEventProcessor> eventProcessor, IKeyStateTracker tracker,    CursorManager cursorManager,
            IControllerState controllerState, DelayMovementUpState delayMovementUpState)
        {
            _eventProcessor = eventProcessor;
            _tracker = tracker;
            _cursorManager = cursorManager;
            State = controllerState; 
            _delayMovementUpState = delayMovementUpState;
            _channel = Channel.CreateBounded<KeyEvent>(new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            _ = Task.Run(() => _eventProcessor.Value.ProcessLoopAsync(_channel.Reader, _tracker));
        }

        public async Task ProcessEventAsync(KeyEvent evt)
        {
            if (!ToggleKeys.Contains(Key.Pa1))
            {
                ToggleKeys.Add(Key.Pa1);
                ToggleKeys.Add(Key.NoName);
                ToggleKeys.Add(Key.Oem102);
            }

            IsClick    = _altMode;
            CurrentKey = evt.Key;

            if (evt.Action == KeyAction.Down)
            {
                if (State.IncomingDirectionalSkill)
                {
                    if (State.AnyOtherWASDIsCurrentlyHeldDown)
                    {
                        await PerformIncomingWasdTaskAsync(true, true);
                        return;
                    }
                }
                
                if (State.IncomingSkill)
                    SkillDownTimes[evt.Key] = DateTime.UtcNow;

                if (State.IncomingSkill && !State.AnyOtherSkillIsCurrentlyHeldDown)
                {
                    await PerformIncomingSkillDownTaskAsync();
                    return;
                }

                if (State.IncomingWASD)
                    await PerformIncomingWasdTaskAsync(true);
            }
            else 
            {
                if (State.IncomingSkill && !State.AnyOtherSkillIsCurrentlyHeldDown)
                {
                    await PerformIncomingSkillUpTaskAsync();
                    if (State.AnyOtherWASDIsCurrentlyHeldDown)
                    {
                        await PerformIncomingWasdTaskAsync(true, State.AnyOtherDirectionalSkillIsCurrentlyHeldDown);
                        return;
                    }
                    return;
                }

                if (State.IncomingDirectionalSkill)
                {
                    if (State.AnyOtherDirectionalSkillIsCurrentlyHeldDown)
                    {
                        
                        return;
                    }
                    else if (State.AnyOtherSkillIsCurrentlyHeldDown)
                    {
                        
                        await PerformIncomingSkillDownTaskAsync(true);
                        return;
                    }
                    else if(State.AnyOtherWASDIsCurrentlyHeldDown)
                    {
                        return;
                    }
                }
                
                if (State.IncomingWASD)
                {

                    if (State.AnyOtherWASDIsCurrentlyHeldDown)
                    {
                        await PerformIncomingWasdTaskAsync();
                    }
                    else
                    {
                        await State.DontMovePlace();
                    }
                        
                }
            }
        }

        private async Task PerformIncomingSkillDownTaskAsync(bool dontMoveOverride = false)
        {
            if (_delayMovementUpState.HasThresholdPassed())
            {
                await State.DontMovePlace();
                _delayMovementUpState.StopMovement(true);
            }

            if (dontMoveOverride)
            {
                await State.DontMovePlace();
                _delayMovementUpState.MovementStarted = false;
            }

            await State.StandInPlace();
            await Task.Delay(16);
            await _cursorManager.UnlockRealCursor(IsClick);
        }
        
        private async Task PerformIncomingSkillUpTaskAsync()
        {
            if (_delayMovementUpState.CanGoUp)
            {

                await State.DontMovePlace();
                _delayMovementUpState.StopMovement();
            }

            await State.DontStandInPlace();
            await _cursorManager.LockRealCursor();
        }

        private async Task PerformIncomingWasdTaskAsync(bool delayBeforeMove = false, bool trumpSkill = false)
        {
            if (State.AnySkillDown && !trumpSkill) return;
            if (trumpSkill)
            {
               await _cursorManager.LockRealCursor();
            }
            await Task.Delay(8);
            await _cursorManager.SetDirectionalCursorPosition(State.CurrentlyHeldWASDKeys);
            if (delayBeforeMove)
                await Task.Delay(24);
            await State.MovePlace();
            _delayMovementUpState.StartMovement();
        }

        public Task HandleKeyDownAsync(Key key, bool altMode) => EnqueueEvent(KeyAction.Down, key, altMode);
        public Task HandleKeyUpAsync  (Key key, bool altMode) => EnqueueEvent(KeyAction.Up,   key, altMode);

        private Task EnqueueEvent(KeyAction action, Key key, bool altMode)
        {
            _altMode = altMode;
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var evt = new KeyEvent(action, key, tcs);
            if (!_channel.Writer.TryWrite(evt))
                tcs.SetResult(null);
            return tcs.Task;
        }
    }
}
