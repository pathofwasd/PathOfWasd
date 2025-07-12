using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PathOfWASD.Overlays.Cursor.Interfaces;

namespace PathOfWASD.Overlays.Cursor;

public class CursorImageLoader : ICursorImageLoader
{
    private const string FileName  = "cursor.png";
    private const string FolderName = "PathOfWASD";
    private static readonly string _folder =
        Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            FolderName);
    private static readonly string _targetPath =
        Path.Combine(_folder, FileName);

    public ImageSource Load()
    {
        if (!File.Exists(_targetPath)) return null;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption   = BitmapCacheOption.OnLoad;          
        bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache; 
        bmp.UriSource     = new Uri(_targetPath, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public ImageSource LoadCursorImage()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder  = Path.Combine(appData, FolderName);
        var path    = Path.Combine(folder, FileName);
        return File.Exists(path)
            ? new BitmapImage(new Uri(path, UriKind.Absolute))
            : null;
    }
    
    public void SaveFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        if (!string.Equals(Path.GetExtension(filePath), ".png",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .png files are allowed.");

        Directory.CreateDirectory(_folder);     
        File.Copy(filePath, _targetPath, true); 
    }
}