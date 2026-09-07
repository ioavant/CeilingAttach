using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CeilingAttach
{
    internal static class ToastWindow
    {
        internal static void Show(string title, string message, double autoCloseSeconds)
        {
            var tb = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };

            var w = new Window
            {
                Title = title,
                Width = 340,
                SizeToContent = SizeToContent.Height,
                MinHeight = 80,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = false,
                Content = tb
            };

            // DispatcherTimer fires on the UI thread inside ShowDialog's nested
            // message loop — safe to call w.Close() directly from the tick handler.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSeconds) };
            timer.Tick += (s, e) => { timer.Stop(); w.Close(); };
            w.Loaded += (s, e) => timer.Start();

            w.ShowDialog();
        }
    }
}
