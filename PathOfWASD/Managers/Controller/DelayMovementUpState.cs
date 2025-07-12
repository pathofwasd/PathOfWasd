namespace PathOfWASD.Managers.Controller;

public class DelayMovementUpState
{
    public bool MovementStarted { get; set; }
    public bool CanGoUp { get; private set; }
    public DateTime MovementStartTime { get; private set; }
    
    private readonly TimeSpan _moveThreshold = TimeSpan.FromMilliseconds(600); 

    public void StartMovement()
    {
        MovementStarted = true;
        CanGoUp = false;
        MovementStartTime = DateTime.UtcNow;
    }

    public void StopMovement(bool allowGoUp = false)
    {
        MovementStarted = false;
        CanGoUp = allowGoUp;
    }

    public void Clear()
    {
        MovementStarted = false;
        CanGoUp = false;
        MovementStartTime = default;
    }

    public bool HasThresholdPassed()
        => MovementStarted && (DateTime.UtcNow - MovementStartTime) >= _moveThreshold;
}