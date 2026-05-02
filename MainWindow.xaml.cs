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
        private readonly SolidColorBrush _activeBgBrush = new SolidColorBrush(Color.FromArgb(51, 81, 39, 173));  // #335127AD (20% Opacity)
        private readonly SolidColorBrush _activeFgBrush = new SolidColorBrush(Color.FromArgb(255, 116, 209, 255)); // #74D1FF

        private readonly SolidColorBrush _inactiveBgBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));       // Transparent
        private readonly SolidColorBrush _inactiveFgBrush = new SolidColorBrush(Color.FromArgb(255, 190, 200, 207)); // #BEC8CF

        public MainWindow()
        {
            this.InitializeComponent();

            // 1. Remove the default title bar and extend our XAML into that space
            ExtendsContentIntoTitleBar = true;

            // 2. Color the system window controls (Minimize, Maximize, Close) 
            // to match the Ethereal "Atmospheric Desktop" design system.
            SetTitleBarButtonColors();
        }

        private void SetTitleBarButtonColors()
        {
            var titleBar = AppWindow.TitleBar;

            // Base background color matching the Level 0 Surface (#131313)
            var surfaceColor = Windows.UI.Color.FromArgb(255, 19, 19, 19);

            // Hover background color matching Level 3 Elevated (#353535)
            var surfaceContainerHighest = Windows.UI.Color.FromArgb(255, 53, 53, 53);

            // Text/Icon color (White)
            var foregroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

            // Apply colors to the standard states
            titleBar.ButtonBackgroundColor = surfaceColor;
            titleBar.ButtonForegroundColor = foregroundColor;

            // Apply colors to the hover states
            titleBar.ButtonHoverBackgroundColor = surfaceContainerHighest;
            titleBar.ButtonHoverForegroundColor = foregroundColor;

            // Apply colors to the pressed states
            titleBar.ButtonPressedBackgroundColor = surfaceColor;
            titleBar.ButtonPressedForegroundColor = foregroundColor;

            // Inactive states (when the app loses focus)
            titleBar.ButtonInactiveBackgroundColor = surfaceColor;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 120, 120, 120);
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            ResetButtonStyles();

            if(sender is Button clickedBtn)
            {
                clickedBtn.Background = _activeBgBrush;
                clickedBtn.Foreground = _activeFgBrush;

                switch(clickedBtn.Name)
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
            }
        }

        private void ResetButtonStyles()
        {
            Button[] navButtons = { LibraryBtn, ReadingBtn, RecentsBtn, SettingsBtn };
            foreach(var btn in navButtons)
            {
                btn.Foreground = _inactiveFgBrush;
                btn.Background = _inactiveBgBrush;
            }
        }
    }
}