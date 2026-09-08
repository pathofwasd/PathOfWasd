using PathOfWASD.Managers.Cursor;

namespace PathOfWASD.Startup;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == CursorVisibilityWatchdog.Argument)
        {
            CursorVisibilityWatchdog.Run();
            return;
        }
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
