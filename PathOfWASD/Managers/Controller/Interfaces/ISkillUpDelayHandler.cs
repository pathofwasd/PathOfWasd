namespace PathOfWASD.Managers.Controller.Interfaces;

public interface ISkillUpDelayHandler
{
    Task DelaySkillUpAsync(KeyEvent evt, CancellationTokenSource cts, IKeyStateTracker tracker);
}