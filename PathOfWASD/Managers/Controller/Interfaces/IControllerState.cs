using System.Windows.Input;
using WindowsInput.Native;

namespace PathOfWASD.Managers.Controller.Interfaces;

public interface IControllerState
{
    bool IncomingSkill { get; }
    bool IncomingDirectionalSkill { get; }
    bool IncomingWASD { get; }
    bool AnySkillDown { get; }
    bool AnyDirectionalSkillDown { get; }
    List<Key> CurrentlyHeldSkillKeys { get; }
    List<Key> CurrentlyHeldWASDKeys { get; }
    bool AnyOtherSkillIsCurrentlyHeldDown { get; }
    bool AnyOtherDirectionalSkillIsCurrentlyHeldDown { get; }
    bool AnyOtherWASDIsCurrentlyHeldDown { get; }
    List<Key> AllHeldSkillDown(List<Key> toggleKeys);
    VirtualKeyCode StandKey { get; set; }
    VirtualKeyCode MovementKey { get; set; }
    Task StandInPlace();
    Task DontStandInPlace();
    Task MovePlace();
    Task DontMovePlace();
}