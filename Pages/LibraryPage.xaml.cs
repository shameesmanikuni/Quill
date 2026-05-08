using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Quill.ViewModels;
using System;

namespace Quill.Pages
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; } = new LibraryViewModel();
        private bool _sectionHovered = false;
        private const double CardScrollWidth = 512;

        public LibraryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.Frame.ForwardStack.Clear();
            try { await ViewModel.LoadAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load books: {ex.Message}"); }
        }

        // ════════════════════════════════════════════════════════════════════
        // ASPECT RATIO HANDLER 
        // ════════════════════════════════════════════════════════════════════
        private void Cover_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                double targetHeight = e.NewSize.Width * 1.3333;
                if (double.IsNaN(element.Height) || Math.Abs(element.Height - targetHeight) > 1.0)
                {
                    element.Height = targetHeight;
                }
            }
        }

        private void Cover_Loaded(object sender, RoutedEventArgs e)
        {
            // ItemsRepeater virtualization safety net: forces the 3:4 aspect ratio the moment the item appears
            if (sender is FrameworkElement element && element.ActualWidth > 0)
            {
                double targetHeight = element.ActualWidth * 1.3333;
                if (double.IsNaN(element.Height) || Math.Abs(element.Height - targetHeight) > 1.0)
                {
                    element.Height = targetHeight;
                }
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
            if (_sectionHovered) UpdateArrowVisibility();
        }

        private void UpdateArrowVisibility()
        {
            if (!ViewModel.ShowContinueReadingArrows) return;
            var scroll = ContinueReadingScroll;
            LeftArrowBtn.Opacity = scroll.HorizontalOffset <= 1 ? 0.25 : 1.0;
            RightArrowBtn.Opacity = Math.Abs(scroll.HorizontalOffset - scroll.ScrollableWidth) < 1 ? 0.25 : 1.0;
        }

        private void LeftArrow_Click(object sender, RoutedEventArgs e) =>
            ContinueReadingScroll.ChangeView(Math.Max(0, ContinueReadingScroll.HorizontalOffset - CardScrollWidth), null, null);

        private void RightArrow_Click(object sender, RoutedEventArgs e) =>
            ContinueReadingScroll.ChangeView(ContinueReadingScroll.HorizontalOffset + CardScrollWidth, null, null);

        // ════════════════════════════════════════════════════════════════════
        // HOVER EFFECTS & ANIMATIONS (OPTIMIZED)
        // ════════════════════════════════════════════════════════════════════
        private void ContinueReadingCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && FindVisualChild<Image>(btn, "CoverImageElement")?.RenderTransform is ScaleTransform st)
            {
                Canvas.SetZIndex(btn, 100); // Prevent clipping
                AnimateDouble(st, "ScaleX", 1.05, 400);
                AnimateDouble(st, "ScaleY", 1.05, 400);
            }
        }

        private void ContinueReadingCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && FindVisualChild<Image>(btn, "CoverImageElement")?.RenderTransform is ScaleTransform st)
            {
                Canvas.SetZIndex(btn, 0);
                AnimateDouble(st, "ScaleX", 1.0, 400);
                AnimateDouble(st, "ScaleY", 1.0, 400);
            }
        }

        private void LibraryCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // FIXED: Forces the hovered card to the absolute front so it doesn't slide under the UI above it
                Canvas.SetZIndex(btn, 100);

                if (FindVisualChild<TextBlock>(btn, "BookTitleText") is TextBlock text) AnimateTextColor(text, Windows.UI.Color.FromArgb(255, 117, 209, 255), 300);
                if (FindVisualChild<Border>(btn, "CoverShadow") is Border shadow) AnimateDouble(shadow, "Opacity", 1.0, 300);

                if (FindVisualChild<Grid>(btn, "CoverContainer") is Grid cover)
                {
                    EnsureTranslateTransform(cover);
                    AnimateDouble(cover.RenderTransform, "Y", -8, 300);
                }
            }
        }

        private void LibraryCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                Canvas.SetZIndex(btn, 0); // Reset depth

                if (FindVisualChild<TextBlock>(btn, "BookTitleText") is TextBlock text) AnimateTextColor(text, Windows.UI.Color.FromArgb(255, 229, 226, 225), 300);
                if (FindVisualChild<Border>(btn, "CoverShadow") is Border shadow) AnimateDouble(shadow, "Opacity", 0.0, 300);

                if (FindVisualChild<Grid>(btn, "CoverContainer") is Grid cover)
                {
                    EnsureTranslateTransform(cover);
                    AnimateDouble(cover.RenderTransform, "Y", 0, 300);
                }
            }
        }

        // OPTIMIZATION: One unified method for Opacity, Translate Y, and Scale animations
        private void AnimateDouble(DependencyObject target, string propertyPath, double toValue, int durationMs)
        {
            var storyboard = new Storyboard();
            var anim = new DoubleAnimation
            {
                To = toValue,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, propertyPath);
            storyboard.Children.Add(anim);
            storyboard.Begin();
        }

        private void AnimateTextColor(TextBlock target, Windows.UI.Color toColor, int durationMs)
        {
            if (target.Foreground is not SolidColorBrush brush) { brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 226, 225)); target.Foreground = brush; }
            var storyboard = new Storyboard();
            var anim = new ColorAnimation { To = toColor, Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, EnableDependentAnimation = true };
            Storyboard.SetTarget(anim, brush);
            Storyboard.SetTargetProperty(anim, "Color");
            storyboard.Children.Add(anim);
            storyboard.Begin();
        }

        private static void EnsureTranslateTransform(FrameworkElement element)
        {
            if (element.RenderTransform is not TranslateTransform)
            {
                element.RenderTransform = new TranslateTransform();
                element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ACTIONS & NAVIGATION
        // ════════════════════════════════════════════════════════════════════
        public async void RefreshAfterImport() => await ViewModel.RefreshAsync();

        private async void DeleteBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Quill.Models.Book book)
            {
                await Quill.Services.LibraryService.Instance.DeleteBookAsync(book.Id);
                await ViewModel.RefreshAsync();
            }
        }

        private async void EditBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Quill.Models.Book book)
            {
                var inputBox = new TextBox { PlaceholderText = "Enter new title", Text = book.Title, SelectionStart = 0, SelectionLength = book.Title?.Length ?? 0 };
                var dialog = new ContentDialog { Title = "Edit Book", PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, XamlRoot = this.XamlRoot, Content = inputBox };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var newTitle = inputBox.Text?.Trim();
                    if (!string.IsNullOrEmpty(newTitle) && newTitle != book.Title)
                    {
                        await Quill.Services.LibraryService.Instance.RenameBookAsync(book.Id, newTitle);
                        await ViewModel.RefreshAsync();
                    }
                }
            }
        }

        private void BookCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Quill.Models.Book book)
                this.Frame.Navigate(typeof(Quill.Pages.ReaderPage), book, new SuppressNavigationTransitionInfo());
        }

        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : class
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name && child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}