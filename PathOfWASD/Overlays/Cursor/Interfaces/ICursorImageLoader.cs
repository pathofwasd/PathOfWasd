using System.Windows.Media;

namespace PathOfWASD.Overlays.Cursor.Interfaces;

public interface ICursorImageLoader
{
    ImageSource Load();          
    void SaveFromFile(string filePath);   
}