using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace musicplayer.View.UserControls
{
    public partial class MenuBar : UserControl
    {
        public MenuBar()
        {
            InitializeComponent();
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                if (FindParent<Button>(source) != null)
                    return;
            }

            Window? window = Window.GetWindow(this);

            if (window == null)
                return;

            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore(window);
                e.Handled = true;
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (window.WindowState == WindowState.Maximized)
            {
                Point mousePosition = PointToScreen(e.GetPosition(this));

                double percentAcrossWindow = mousePosition.X / Math.Max(window.ActualWidth, 1);

                window.WindowState = WindowState.Normal;

                window.Left = mousePosition.X - window.Width * percentAcrossWindow;
                window.Top = mousePosition.Y - 20;
            }

            try
            {
                window.DragMove();
            }
            catch
            {
            }
        }

        private void ToggleMaximizeRestore(Window window)
        {
            if (window.WindowState == WindowState.Maximized)
                window.WindowState = WindowState.Normal;
            else
                window.WindowState = WindowState.Maximized;
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            Window? window = Window.GetWindow(this);

            if (window != null)
                window.WindowState = WindowState.Minimized;
        }

        private void btnMaximize_Click(object sender, RoutedEventArgs e)
        {
            Window? window = Window.GetWindow(this);

            if (window == null)
                return;

            ToggleMaximizeRestore(window);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}