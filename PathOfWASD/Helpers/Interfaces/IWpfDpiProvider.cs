namespace PathOfWASD.Helpers.Interfaces;

public interface IWpfDpiProvider
{
    (double ScaleX, double ScaleY) GetDpi();
}