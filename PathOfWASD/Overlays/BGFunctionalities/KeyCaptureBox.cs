using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PathOfWASD.Overlays.BGFunctionalities
{
    public class KeyCaptureBox : TextBox
    {
        static KeyCaptureBox()
        {
        }

        public static readonly DependencyProperty SelectedKeyProperty =
            DependencyProperty.Register(
                nameof(SelectedKey),
                typeof(Key?),
                typeof(KeyCaptureBox),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedKeyChanged));   

        public Key? SelectedKey
        {
            get => (Key?)GetValue(SelectedKeyProperty);
            set => SetValue(SelectedKeyProperty, value);
        }

        public KeyCaptureBox()
        {
            IsReadOnly = true;
            Focusable = true;
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;

            Background = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)); 
            Foreground = Brushes.White;
            BorderBrush = new SolidColorBrush(Color.FromRgb(136, 136, 136)); 
            BorderThickness = new Thickness(1);
            Padding = new Thickness(6, 2, 6, 2);
            FontSize = 13;
            CaretBrush = Brushes.White;
            Cursor = Cursors.Hand;
            CornerRadius radius = new CornerRadius(4); 

            GotKeyboardFocus += (_, __) =>
            {
                Text = "Press a key...";
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            };

            PreviewKeyDown += (_, e) =>
            {
                SelectedKey = e.Key; 
                Keyboard.ClearFocus();
                e.Handled = true;
            };

            LostKeyboardFocus += (_, __) =>
            {
                if (SelectedKey.HasValue)
                {
                    Text = SelectedKey.Value.ToString();
                    Foreground = Brushes.White;
                }
                else
                {
                    Text = string.Empty;
                    Foreground = Brushes.White;
                }
            };
        }


        private static void OnSelectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (KeyCaptureBox)d;
            var newKey = (Key?)e.NewValue;

            if (newKey.HasValue)
            {
                box.Text = newKey.Value.ToString();
                box.Foreground = Brushes.White;
            }
            else
            {
                box.Text = string.Empty;
                box.Foreground = Brushes.White;
            }
        }
    }
}