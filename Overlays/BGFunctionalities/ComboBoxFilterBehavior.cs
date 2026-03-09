using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PathOfWASD.Overlays.BGFunctionalities;

public static class ComboBoxCommitBehavior
    {
        public static readonly DependencyProperty CommitOnEnterProperty =
            DependencyProperty.RegisterAttached(
                "CommitOnEnter",
                typeof(bool),
                typeof(ComboBoxCommitBehavior),
                new PropertyMetadata(false, OnCommitOnEnterChanged));

        public static void SetCommitOnEnter(DependencyObject element, bool value) =>
            element.SetValue(CommitOnEnterProperty, value);

        public static bool GetCommitOnEnter(DependencyObject element) =>
            (bool)element.GetValue(CommitOnEnterProperty);

        private static void OnCommitOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox cb) return;

            if ((bool)e.NewValue)
            {
                cb.PreviewKeyDown += ComboBoxOnPreviewKeyDown;
                cb.LostFocus     += ComboBoxOnLostFocus;
            }
            else
            {
                cb.PreviewKeyDown -= ComboBoxOnPreviewKeyDown;
                cb.LostFocus     -= ComboBoxOnLostFocus;
            }
        }

        private static void ComboBoxOnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Commit((ComboBox)sender);
        }

        private static void ComboBoxOnLostFocus(object sender, RoutedEventArgs e)
        {
            Commit((ComboBox)sender);
        }

        private static void Commit(ComboBox cb)
        {
            var txt = cb.Text?.Trim();
            if (string.IsNullOrEmpty(txt)) return;

            var match = cb.Items
                          .Cast<object>()
                          .FirstOrDefault(item =>
                              string.Equals(item.ToString(),
                                            txt,
                                            StringComparison.OrdinalIgnoreCase));
            if (match != null)
                cb.SelectedItem = match;
        }
    }