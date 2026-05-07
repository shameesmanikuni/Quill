using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.UI;

namespace Quill
{
    public sealed partial class MainWindow : Window
    {
        // 1. Singleton instance so ReaderPage can hide the Navigation UI
        public static MainWindow? Instance { get; private set; }

        private readonly SolidColorBrush _activeBgBrush = new SolidColorBrush(Color.FromArgb(51, 81, 39, 173));
        private readonly SolidColorBrush _activeBgHoverBrush = new SolidColorBrush(Color.FromArgb(80, 81, 39, 173));
        private readonly SolidColorBrush _activeFgBrush = new SolidColorBrush(Color.FromArgb(255, 116, 209, 255));
        private readonly SolidColorBrush _inactiveBgBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private readonly SolidColorBrush _inactiveBgHoverBrush = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
        private readonly SolidColorBrush _inactiveFgBrush = new SolidColorBrush(Color.FromArgb(255, 190, 200, 207));

        public MainWindow()
        {
            Instance = this; // Set the instance
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBarButtonColors();
            CustomizeCaptionButtons();

            RootGrid.Loaded += MainWindow_Loaded;
        }

        // 2. New Method to Toggle Fullscreen Reader View
        public void SetReaderMode(bool isReaderMode)
        {
            if (isReaderMode)
            {
                // Hide global UI and remove the frame margin to fill the whole screen
                SidebarGrid.Visibility = Visibility.Collapsed;
                TopBarGrid.Visibility = Visibility.Collapsed;
                ContentFrame.Margin = new Thickness(0);
            }
            else
            {
                // Restore global UI and margin
                SidebarGrid.Visibility = Visibility.Visible;
                TopBarGrid.Visibility = Visibility.Visible;
                ContentFrame.Margin = new Thickness(80, 0, 0, 0);
            }
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
            ResetButtonStyles();

            if (sender is Button clickedBtn)
            {
                clickedBtn.Background = _activeBgBrush;
                clickedBtn.Foreground = _activeFgBrush;
                clickedBtn.Resources["ButtonBackgroundPointerOver"] = _activeBgHoverBrush;
                clickedBtn.Resources["ButtonForegroundPointerOver"] = _activeFgBrush;
                clickedBtn.Resources["ButtonBackgroundPressed"] = _activeBgHoverBrush;
                clickedBtn.Resources["ButtonForegroundPressed"] = _activeFgBrush;

                switch (clickedBtn.Name)
                {
                    case "LibraryBtn":
                        ContentFrame.Navigate(typeof(Pages.LibraryPage));
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
                btn.Background = _inactiveBgBrush;
                btn.Foreground = _inactiveFgBrush;
                btn.Resources["ButtonBackgroundPointerOver"] = _inactiveBgHoverBrush;
                btn.Resources["ButtonForegroundPointerOver"] = _inactiveFgBrush;
                btn.Resources["ButtonBackgroundPressed"] = _inactiveBgHoverBrush;
                btn.Resources["ButtonForegroundPressed"] = _inactiveFgBrush;
            }
        }

        private void CustomizeCaptionButtons()
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = Color.FromArgb(255, 190, 200, 207);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 100, 100, 100);
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(20, 255, 255, 255);
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(40, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Colors.White;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var newBooks = await Quill.Services.LibraryService.Instance.ImportBooksAsync(hwnd);

                if (newBooks != null && newBooks.Count > 0)
                {
                    if (ContentFrame.Content is Pages.LibraryPage libraryPage)
                    {
                        libraryPage.RefreshAfterImport();
                    }
                }
            }
            catch (Quill.Services.DuplicateBookException ex)
            {
                if (ContentFrame.Content is Pages.LibraryPage libraryPage)
                {
                    libraryPage.RefreshAfterImport();
                }
                ShowToast(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import failed: {ex.Message}");
            }
        }

        private async void ShowToast(string message)
        {
            NotificationToast.Message = message;
            NotificationToast.IsOpen = true;
            await Task.Delay(3000);
            NotificationToast.IsOpen = false;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NavButton_Click(LibraryBtn, new RoutedEventArgs());
        }
    }
}