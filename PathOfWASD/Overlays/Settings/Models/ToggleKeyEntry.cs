using System.ComponentModel;
using PathOfWASD.Overlays.BGFunctionalities;
using WindowsInput.Native;

namespace PathOfWASD.Overlays.Settings.Models
{

    public class ToggleKeyEntry : INotifyPropertyChanged
    {
        private KeyPair _keyPair;

        public KeyPair SelectedKey
        {
            get => _keyPair;
            set
            {
                if (_keyPair != value)
                {
                    _keyPair = value;
                    OnPropertyChanged(nameof(SelectedKey));
                }
            }
        }

        private bool _isDirectional;

        public bool IsDirectional
        {
            get => _isDirectional;
            set
            {
                if (_isDirectional != value)
                {
                    _isDirectional = value;
                    OnPropertyChanged(nameof(IsDirectional));
                }
            }
        }

        public ToggleKeyEntry(VirtualKeyCode initialKey, bool isDirectional = false)
        {
            _keyPair = new KeyPair { VirtualKey = initialKey };
            _isDirectional = isDirectional;

            _keyPair.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(KeyPair.DisplayKey) ||
                    e.PropertyName == nameof(KeyPair.VirtualKey))
                {
                    OnPropertyChanged(nameof(SelectedKey));
                }
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}