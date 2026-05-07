using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Quill.ViewModels;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Quill.Pages
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; } = new LibraryViewModel();

        // Scroll behavior
        private bool _sectionHovered = false;
        private const double CardScrollWidth = 512; // 480px card + 32px gap

        public LibraryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        }

        /// <summary>
        /// Fires when navigating to this page.
        /// Load all books from storage.
        /// </summary>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.Frame.ForwardStack.Clear();

            try
            {
                await ViewModel.LoadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load books: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CONTINUE READING SCROLL ARROWS
        // ════════════════════════════════════════════════════════════════════

        private void ContinueReadingSection_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _sectionHovered = true;
            UpdateArrowVisibility();
        }

        private void ContinueReadingSection_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _sectionHovered = false;
            LeftArrowBtn.Opacity = 0;
            RightArrowBtn.Opacity = 0;
        }

        /// <summary>
        /// When scrolling, update arrow opacity based on scroll position.
        /// At start: left arrow faded. At end: right arrow faded.
        /// </summary>
        private void ContinueReadingScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_sectionHovered)
                UpdateArrowVisibility();
        }

        private void UpdateArrowVisibility()
        {
            if (!ViewModel.ShowContinueReadingArrows)
                return;

            var scroll = ContinueReadingScroll;

            // Detect if we're at the extremes
            bool atStart = scroll.HorizontalOffset <= 1;
            bool atEnd = Math.Abs(scroll.HorizontalOffset - scroll.ScrollableWidth) < 1;

            // Faded opacity (0.25) at extremes, normal (1.0) otherwise
            LeftArrowBtn.Opacity = atStart ? 0.25 : 1.0;
            RightArrowBtn.Opacity = atEnd ? 0.25 : 1.0;
        }

        private void LeftArrow_Click(object sender, RoutedEventArgs e)
        {
            // Scroll left by one card width
            var newOffset = Math.Max(0, ContinueReadingScroll.HorizontalOffset - CardScrollWidth);
            ContinueReadingScroll.ChangeView(newOffset, null, null);
        }

        private void RightArrow_Click(object sender, RoutedEventArgs e)
        {
            // Scroll right by one card width
            var newOffset = ContinueReadingScroll.HorizontalOffset + CardScrollWidth;
            ContinueReadingScroll.ChangeView(newOffset, null, null);
        }

        /// <summary>
        /// Called from MainWindow after importing a book.
        /// Refreshes the collections to show the new book.
        /// </summary>
        public async void RefreshAfterImport()
        {
            await ViewModel.RefreshAsync();
        }

        private async void DeleteBook_Click(object sender, RoutedEventArgs e)
        {
            // Get the book object from the button's DataContext
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Quill.Models.Book book)
            {
                await Quill.Services.LibraryService.Instance.DeleteBookAsync(book.Id);
                await ViewModel.RefreshAsync();
            }
        }

        private async void BookCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Quill.Models.Book book)
            {
                // NEW: Yield the UI thread for 50ms so the button's "Pressed" visual animation can finish smoothly!
                await System.Threading.Tasks.Task.Delay(50);

                // Navigate to the reader
                this.Frame.Navigate(typeof(Quill.Pages.ReaderPage), book);
            }
        }
    }
}