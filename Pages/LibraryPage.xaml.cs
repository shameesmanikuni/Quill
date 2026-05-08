using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Quill.ViewModels;
using System;

namespace Quill.Pages
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; } = new LibraryViewModel();
        private bool _sectionHovered = false;
        private bool _isListView = false;
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
        // COVER IMAGE BUG FIX — reload on failure (virtualization cache miss)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles ImageFailed for <Image> elements (Continue Reading cards).
        /// Forces a fresh URI reload so the cover reappears after scroll virtualization.
        /// </summary>
        private void CoverImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (sender is Image img && img.Tag is Quill.Models.Book book && book.HasValidCover)
            {
                try
                {
                    // Let the Uri class safely handle spaces and local pathing
                    var bmp = new BitmapImage();
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.UriSource = new Uri(book.CoverPath, UriKind.Absolute);
                    img.Source = bmp;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cover reload failed: {ex.Message}");
                }
            }
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
                    element.Height = targetHeight;
            }
        }

        private void Cover_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ActualWidth > 0)
            {
                double targetHeight = element.ActualWidth * 1.3333;
                if (double.IsNaN(element.Height) || Math.Abs(element.Height - targetHeight) > 1.0)
                    element.Height = targetHeight;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // GRID / LIST VIEW TOGGLE
        // ════════════════════════════════════════════════════════════════════
        private void ToggleView_Click(object sender, RoutedEventArgs e)
        {
            _isListView = !_isListView;

            if (this.Resources.TryGetValue("SymbolFont", out object resource) && resource is FontFamily symbolFont)
            {
                ToggleViewIcon.FontFamily = symbolFont;
            }

            if (_isListView)
            {
                // Switch to list view
                LibraryGrid.Visibility = Visibility.Collapsed;
                LibraryList.Visibility = Visibility.Visible;
                // E896 = List icon (Segoe MDL2 Assets)
                ToggleViewIcon.Glyph = "\uE896";
            }
            else
            {
                // Switch back to grid view
                LibraryGrid.Visibility = Visibility.Visible;
                LibraryList.Visibility = Visibility.Collapsed;
                // E9B0 = Grid icon
                ToggleViewIcon.Glyph = "\uE9B0";
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
                Canvas.SetZIndex(btn, 100);
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
                Canvas.SetZIndex(btn, 0);
                if (FindVisualChild<TextBlock>(btn, "BookTitleText") is TextBlock text) AnimateTextColor(text, Windows.UI.Color.FromArgb(255, 229, 226, 225), 300);
                if (FindVisualChild<Border>(btn, "CoverShadow") is Border shadow) AnimateDouble(shadow, "Opacity", 0.0, 300);
                if (FindVisualChild<Grid>(btn, "CoverContainer") is Grid cover)
                {
                    EnsureTranslateTransform(cover);
                    AnimateDouble(cover.RenderTransform, "Y", 0, 300);
                }
            }
        }

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
                // ── Modern Edit Dialog ───────────────────────────────────────
                var titleBox = new TextBox
                {
                    PlaceholderText = "Book title",
                    Text = book.Title ?? string.Empty,
                    SelectionStart = 0,
                    SelectionLength = book.Title?.Length ?? 0,
                    FontFamily = new FontFamily("Segoe UI Variable"),
                    FontSize = 15,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 12, 14, 12),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255)),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 226, 225)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255)),
                };

                var authorBox = new TextBox
                {
                    PlaceholderText = "Author name",
                    Text = book.Author ?? string.Empty,
                    FontFamily = new FontFamily("Segoe UI Variable"),
                    FontSize = 15,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 12, 14, 12),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255)),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 226, 225)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255)),
                };

                var titleLabel = new TextBlock
                {
                    Text = "Title",
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 136, 146, 153)),
                    Margin = new Thickness(2, 0, 0, 6),
                    FontFamily = new FontFamily("Segoe UI Variable"),
                };

                var authorLabel = new TextBlock
                {
                    Text = "Author",
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 136, 146, 153)),
                    Margin = new Thickness(2, 0, 0, 6),
                    FontFamily = new FontFamily("Segoe UI Variable"),
                };

                var content = new StackPanel { Spacing = 20, MinWidth = 360 };
                var titleGroup = new StackPanel { Spacing = 0 };
                titleGroup.Children.Add(titleLabel);
                titleGroup.Children.Add(titleBox);
                var authorGroup = new StackPanel { Spacing = 0 };
                authorGroup.Children.Add(authorLabel);
                authorGroup.Children.Add(authorBox);
                content.Children.Add(titleGroup);
                content.Children.Add(authorGroup);

                var dialog = new ContentDialog
                {
                    Title = "Edit Book",
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot,
                    Content = content,
                    // Style the dialog to match the dark theme
                    RequestedTheme = ElementTheme.Dark,
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var newTitle = titleBox.Text?.Trim();
                    var newAuthor = authorBox.Text?.Trim();

                    bool changed = false;

                    if (!string.IsNullOrEmpty(newTitle) && newTitle != book.Title)
                    {
                        book.Title = newTitle;
                        changed = true;
                    }

                    if (!string.IsNullOrEmpty(newAuthor) && newAuthor != book.Author)
                    {
                        book.Author = newAuthor;
                        changed = true;
                    }

                    if (changed)
                    {
                        await Quill.Services.LibraryService.Instance.UpdateBookAsync(book);
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