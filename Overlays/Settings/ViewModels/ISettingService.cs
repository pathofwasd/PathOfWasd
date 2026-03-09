namespace PathOfWASD.Overlays.Settings.ViewModels;

public interface ISettingService
{
    Settings Load();
    void Save(Settings settings); 
}