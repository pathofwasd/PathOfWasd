using System.Windows.Input;
using PathOfWASD.Helpers;

namespace PathOfWASD.Overlays.BGFunctionalities;

public class KeyWatcher
{
    public async Task<bool> WatchKeyAsync(Key key, Action whileHeld, int checkIntervalMs = 50)
    {
        await Task.Run(async () =>
        {
            while (!Helper.IsKeyDown(key))
                await Task.Delay(checkIntervalMs);

            while (Helper.IsKeyDown(key))
            {
                whileHeld?.Invoke();
                await Task.Delay(checkIntervalMs);
            }
        });

        return true;
    }
}
