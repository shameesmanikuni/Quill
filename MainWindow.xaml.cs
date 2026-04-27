using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace EtherealPDF
{
    public sealed partial class MainWindow : Window
    {
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
    }
}