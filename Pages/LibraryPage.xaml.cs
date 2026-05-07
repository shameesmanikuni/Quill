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
using Microsoft.UI.Xaml.Media.Animation;

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

        private void ContinueReadingSection_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _sectionHovered = true;
            UpdateArrowVisibility();
        }

        private void ContinueReadingSection_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _sectionHovered = false;
            LeftArrowBtn.Opacity = 0;
            RightArrowBtn.Opacity = 0;
        }

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
            bool atStart = scroll.HorizontalOffset <= 1;
            bool atEnd = Math.Abs(scroll.HorizontalOffset - scroll.ScrollableWidth) < 1;

            LeftArrowBtn.Opacity = atStart ? 0.25 : 1.0;
            RightArrowBtn.Opacity = atEnd ? 0.25 : 1.0;
        }

        private void LeftArrow_Click(object sender, RoutedEventArgs e)
        {
            var newOffset = Math.Max(0, ContinueReadingScroll.HorizontalOffset - CardScrollWidth);
            ContinueReadingScroll.ChangeView(newOffset, null, null);
        }

        private void RightArrow_Click(object sender, RoutedEventArgs e)
        {
            var newOffset = ContinueReadingScroll.HorizontalOffset + CardScrollWidth;
            ContinueReadingScroll.ChangeView(newOffset, null, null);
        }

        // ════════════════════════════════════════════════════════════════════
        // THUMBNAIL ZOOM ANIMATION (MATCHING CODE.HTML)
        // ════════════════════════════════════════════════════════════════════

        private void ContinueReadingCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // The '?' here handles the case where imageElement might be null
                var imageElement = FindVisualChild<Image>(btn, "CoverImageElement");

                if (imageElement?.RenderTransform is ScaleTransform scaleTransform)
                {
                    AnimateScale(scaleTransform, 1.05, 500);
                }
            }
        }

        private void ContinueReadingCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var imageElement = FindVisualChild<Image>(btn, "CoverImageElement");
                if (imageElement?.RenderTransform is ScaleTransform scaleTransform)
                {
                    AnimateScale(scaleTransform, 1.0, 500);
                }
            }
        }

        private void AnimateScale(ScaleTransform target, double toScale, int durationMs)
        {
            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

            var animX = new DoubleAnimation { To = toScale, Duration = duration, EnableDependentAnimation = true };
            Storyboard.SetTarget(animX, target);
            Storyboard.SetTargetProperty(animX, "ScaleX");

            var animY = new DoubleAnimation { To = toScale, Duration = duration, EnableDependentAnimation = true };
            Storyboard.SetTarget(animY, target);
            Storyboard.SetTargetProperty(animY, "ScaleY");

            storyboard.Children.Add(animX);
            storyboard.Children.Add(animY);
            storyboard.Begin();
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS & NAVIGATION
        // ════════════════════════════════════════════════════════════════════

        public async void RefreshAfterImport()
        {
            await ViewModel.RefreshAsync();
        }

        private async void DeleteBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Quill.Models.Book book)
            {
                await Quill.Services.LibraryService.Instance.DeleteBookAsync(book.Id);
                await ViewModel.RefreshAsync();
            }
        }

        private void BookCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Quill.Models.Book book)
            {
                this.Frame.Navigate(
                    typeof(Quill.Pages.ReaderPage),
                    book,
                    new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo()
                );
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : class
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name && child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}