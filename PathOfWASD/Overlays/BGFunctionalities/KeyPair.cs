using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PathOfWASD.Helpers;
using WindowsInput.Native;

namespace PathOfWASD.Overlays.BGFunctionalities;

public class KeyPair : INotifyPropertyChanged
{
    private VirtualKeyCode _vk;
    private Key?             _wpfKey;

    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string n = null!)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public VirtualKeyCode VirtualKey
    {
        get => _vk;
        set
        {
            if (_vk == value) return;
            _vk = value;
            _wpfKey = Helper.ToVkKey(_vk);
            Raise(nameof(VirtualKey));
            Raise(nameof(DisplayKey));
        }
    }

    public Key? DisplayKey
    {
        get => _wpfKey;
        set
        {
            if (_wpfKey == value) return;
            _wpfKey = value;
            if (value.HasValue)
                _vk = Helper.ToWinFormsKey(value.Value);
            Raise(nameof(DisplayKey));
            Raise(nameof(VirtualKey));
        }
    }

    public override string ToString() 
        => DisplayKey?.ToString() ?? string.Empty;
}