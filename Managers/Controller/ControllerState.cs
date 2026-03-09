using System.Windows.Forms;
using System.Windows.Input;
using PathOfWASD.Helpers;
using PathOfWASD.Managers.Controller.Interfaces;
using WindowsInput;
using WindowsInput.Native;

namespace PathOfWASD.Managers.Controller;

/// <summary>
/// Exposes the derived controller state used by the controller event pipeline.
/// </summary>
public class ControllerState : IControllerState
{
    private readonly IKeyStateTracker _tracker;
    private readonly Lazy<ControllerManager> _manager;
    private readonly InputSimulator _sim = new();
    
    private static readonly Key[] WASDKeys = { Key.W, Key.A, Key.S, Key.D };

    /// <summary>
    /// Creates the controller state wrapper around held-key tracking and manager state.
    /// </summary>
    public ControllerState(IKeyStateTracker tracker, Lazy<ControllerManager> manager)
    {
        _tracker = tracker;
        _manager = manager;
    }

    /// <summary>
    /// Holds the stand-still key.
    /// </summary>
    public async Task StandInPlace()
    {
        _sim.Keyboard.KeyDown(StandKey);
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Releases the stand-still key.
    /// </summary>
    public async Task DontStandInPlace()
    {
        _sim.Keyboard.KeyUp(StandKey);
    }
    
    /// <summary>
    /// Holds the movement key when at least one WASD key is active.
    /// </summary>
    public async Task MovePlace()
    {
        if (Helper.IsWasdKeyDown())
        {
            _sim.Keyboard.KeyDown(MovementKey);
        }
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Releases the movement key.
    /// </summary>
    public async Task DontMovePlace()
    {
        _sim.Keyboard.KeyUp(MovementKey);
        await Task.CompletedTask;
    }
    public VirtualKeyCode StandKey { get; set; }
    public VirtualKeyCode MovementKey { get; set; }
    
    public bool IncomingSkill => _manager.Value.ToggleKeys.Contains(_manager.Value.CurrentKey);
    public bool IncomingDirectionalSkill => _manager.Value.DirectionalKeys.Contains(_manager.Value.CurrentKey);
    public bool IncomingWASD => WASDKeys.Contains(_manager.Value.CurrentKey);
    public bool AnySkillDown => _tracker.AnyHeld(_manager.Value.ToggleKeys);
    public bool AnyDirectionalSkillDown => _tracker.HeldWhere(_manager.Value.DirectionalKeys).Any();
    public List<Key> CurrentlyHeldSkillKeys => _tracker.HeldWhere(_manager.Value.ToggleKeys);
    public List<Key> CurrentlyHeldWASDKeys => _tracker.HeldWhere(WASDKeys);
    public bool AnyOtherWASDIsCurrentlyHeldDown  => CurrentlyHeldWASDKeys.Any();

    public List<Key> AllHeldSkillDown(List<Key> toggleKeys)
        => _tracker
            .HeldWhere(toggleKeys);
    public bool AnyOtherSkillIsCurrentlyHeldDown => _tracker
        .HeldWhere(_manager.Value.ToggleKeys)
        .Any(k => k != _manager.Value.CurrentKey);
    public bool AnyOtherDirectionalSkillIsCurrentlyHeldDown => _tracker
        .HeldWhere(_manager.Value.DirectionalKeys)
        .Any(k => k != _manager.Value.CurrentKey);
}
