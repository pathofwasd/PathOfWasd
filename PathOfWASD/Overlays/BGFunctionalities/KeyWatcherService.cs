using System.Windows.Input;
using PathOfWASD.Helpers;
using PathOfWASD.Internals;

namespace PathOfWASD.Overlays.BGFunctionalities;

public class KeyWatcher
{
    private bool _isWatching = false;

    public async Task<bool> WatchKeyAsync(Key key, Action whileHeld, int checkIntervalMs = 50)
    {
        await Task.Run(async () =>
        {
            while (!Helper.IsKeyDown(key))
                await Task. Delay(checkIntervalMs);

            while (Helper.IsKeyDown(key))
            {
                whileHeld?.Invoke();
                await Task. Delay(checkIntervalMs);
            }
        });

        return true; 
    }
}