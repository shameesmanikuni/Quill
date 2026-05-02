using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EtherealPDF
{
    public sealed partial class MainWindow : Window
    {
        // --- ACTIVE STATE COLORS ---
        // Base: #335127AD (20% Opacity)
        private readonly SolidColorBrush _activeBgBrush = new SolidColorBrush(Color.FromArgb(51, 81, 39, 173));
        // Hover: Brighter Purple (31% Opacity) - Brightens without changing color
        private readonly SolidColorBrush _activeBgHoverBrush = new SolidColorBrush(Color.FromArgb(80, 81, 39, 173));
        // Foreground: Blue
        private readonly SolidColorBrush _activeFgBrush = new SolidColorBrush(Color.FromArgb(255, 116, 209, 255));

        // --- INACTIVE STATE COLORS ---
        // Base: Transparent
        private readonly SolidColorBrush _inactiveBgBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        // Hover: Very subtle white (6% Opacity) - Gives a slight highlight to unselected items
        private readonly SolidColorBrush _inactiveBgHoverBrush = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
        // Foreground: Greyish
        private readonly SolidColorBrush _inactiveFgBrush = new SolidColorBrush(Color.FromArgb(255, 190, 200, 207));

        public MainWindow()
        {
            this.InitializeComponent();

            // 1. Title bar setup
            ExtendsContentIntoTitleBar = true;
            SetTitleBarButtonColors();

            // 2. Set default active button on startup (fixes the first-launch issue)
            NavButton_Click(LibraryBtn, new RoutedEventArgs());
        }

        private void SetTitleBarButtonColors()
        {
            var titleBar = AppWindow.TitleBar;

            var surfaceColor = Color.FromArgb(255, 19, 19, 19);
            var surfaceContainerHighest = Color.FromArgb(255, 53, 53, 53);
            var foregroundColor = Color.FromArgb(255, 255, 255, 255);

            titleBar.ButtonBackgroundColor = surfaceColor;
            titleBar.ButtonForegroundColor = foregroundColor;
            titleBar.ButtonHoverBackgroundColor = surfaceContainerHighest;
            titleBar.ButtonHoverForegroundColor = foregroundColor;
            titleBar.ButtonPressedBackgroundColor = surfaceColor;
            titleBar.ButtonPressedForegroundColor = foregroundColor;
            titleBar.ButtonInactiveBackgroundColor = surfaceColor;
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 120, 120, 120);
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset all buttons to inactive first
            ResetButtonStyles();

            if (sender is Button clickedBtn)
            {
                // 1. Set the standard Active colors
                clickedBtn.Background = _activeBgBrush;
                clickedBtn.Foreground = _activeFgBrush;

                // 2. Set the custom Active Hover colors
                clickedBtn.Resources["ButtonBackgroundPointerOver"] = _activeBgHoverBrush;
                clickedBtn.Resources["ButtonForegroundPointerOver"] = _activeFgBrush;

                // Keep it consistent when clicked/pressed
                clickedBtn.Resources["ButtonBackgroundPressed"] = _activeBgHoverBrush;
                clickedBtn.Resources["ButtonForegroundPressed"] = _activeFgBrush;

                // Routing logic
                switch (clickedBtn.Name)
                {
                    case "LibraryBtn":
                        break;
                    case "ReadingBtn":
                        break;
                    case "RecentsBtn":
                        break;
                    case "SettingsBtn":
                        break;
                }

                VisualStateManager.GoToState(clickedBtn, "Normal", false);
                VisualStateManager.GoToState(clickedBtn, "PointerOver", false);
            }
        }

        private void ResetButtonStyles()
        {
            Button[] navButtons = { LibraryBtn, ReadingBtn, RecentsBtn, SettingsBtn };

            foreach (var btn in navButtons)
            {
                // 1. Set the standard Inactive colors
                btn.Background = _inactiveBgBrush;
                btn.Foreground = _inactiveFgBrush;

                // 2. Set the custom Inactive Hover colors
                btn.Resources["ButtonBackgroundPointerOver"] = _inactiveBgHoverBrush;
                btn.Resources["ButtonForegroundPointerOver"] = _inactiveFgBrush;

                // Keep it consistent when clicked/pressed
                btn.Resources["ButtonBackgroundPressed"] = _inactiveBgHoverBrush;
                btn.Resources["ButtonForegroundPressed"] = _inactiveFgBrush;
            }
        }
    }
}