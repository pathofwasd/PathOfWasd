using System.Windows;
using System.Windows.Media;
using PathOfWASD.Helpers.Interfaces;

namespace PathOfWASD.Helpers;

public class WpfDpiProvider : IWpfDpiProvider
{
    public (double ScaleX, double ScaleY) GetDpi()
    {
        var mainWin = Application.Current.MainWindow;
        if (mainWin == null) return (1.0, 1.0);
        var dpi = VisualTreeHelper.GetDpi(mainWin);
        return (dpi.DpiScaleX, dpi.DpiScaleY);
    }
}