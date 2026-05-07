using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Quill.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Quill.Pages
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; } = new LibraryViewModel();

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
        // CONTINUE READING — THUMBNAIL ZOOM
        // 400ms as calibrated by you. CubicEase EaseOut for smooth snap.
        // Matches code.html img scale hover effect.
        // ════════════════════════════════════════════════════════════════════

        private void ContinueReadingCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var imageElement = FindVisualChild<Image>(btn, "CoverImageElement");
                if (imageElement?.RenderTransform is ScaleTransform scaleTransform)
                {
                    AnimateScale(scaleTransform, 1.05, 400);
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
                    AnimateScale(scaleTransform, 1.0, 400);
                }
            }
        }

        private void AnimateScale(ScaleTransform target, double toScale, int durationMs)
        {
            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var animX = new DoubleAnimation
            {
                To = toScale,
                Duration = duration,
                EnableDependentAnimation = true,
                EasingFunction = easing
            };
            Storyboard.SetTarget(animX, target);
            Storyboard.SetTargetProperty(animX, "ScaleX");

            var animY = new DoubleAnimation
            {
                To = toScale,
                Duration = duration,
                EnableDependentAnimation = true,
                EasingFunction = easing
            };
            Storyboard.SetTarget(animY, target);
            Storyboard.SetTargetProperty(animY, "ScaleY");

            storyboard.Children.Add(animX);
            storyboard.Children.Add(animY);
            storyboard.Begin();
        }

        // ════════════════════════════════════════════════════════════════════
        // LIBRARY GRID — HOVER EFFECTS
        //
        // On hover:
        //   1. Title text color → #75D1FF  (matches code.html group-hover:text-primary-fixed-dim)
        //   2. CoverContainer gets a TranslateTransform applied and animated to Y=-8
        //      (only the thumbnail moves, text stays put)
        //
        // The XAML VisualStateManager in LibraryCardStyle handles the CoverGrid overlay
        // (the transparent lift layer + shadow). This code-behind handles:
        //   - The actual CoverContainer inside ContentPresenter (the visible image)
        //   - The title text color transition
        //
        // Both use QuadraticEase EaseOut at 300ms — the same as the XAML storyboard —
        // for a consistent, calm animation matching code.html duration-300.
        // ════════════════════════════════════════════════════════════════════

        private void LibraryCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // 1. Animate title color to blue
                var titleText = FindVisualChild<TextBlock>(btn, "BookTitleText");
                if (titleText != null)
                {
                    AnimateTextColor(titleText, Windows.UI.Color.FromArgb(255, 117, 209, 255), 300);
                }

                // 2. Animate cover container lift (only cover moves, not the text)
                var coverContainer = FindVisualChild<Grid>(btn, "CoverContainer");
                if (coverContainer != null)
                {
                    EnsureTranslateTransform(coverContainer);
                    if (coverContainer.RenderTransform is TranslateTransform tt)
                    {
                        AnimateTranslateY(tt, -8, 300);
                    }
                }
            }
        }

        private void LibraryCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // 1. Animate title color back to default
                var titleText = FindVisualChild<TextBlock>(btn, "BookTitleText");
                if (titleText != null)
                {
                    AnimateTextColor(titleText, Windows.UI.Color.FromArgb(255, 229, 226, 225), 300);
                }

                // 2. Animate cover container back down
                var coverContainer = FindVisualChild<Grid>(btn, "CoverContainer");
                if (coverContainer != null)
                {
                    EnsureTranslateTransform(coverContainer);
                    if (coverContainer.RenderTransform is TranslateTransform tt)
                    {
                        AnimateTranslateY(tt, 0, 300);
                    }
                }
            }
        }

        /// <summary>
        /// Ensures the element has a TranslateTransform set on RenderTransform.
        /// Preserves existing TranslateTransform if already present.
        /// </summary>
        private static void EnsureTranslateTransform(FrameworkElement element)
        {
            if (element.RenderTransform is not TranslateTransform)
            {
                element.RenderTransform = new TranslateTransform();
                element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            }
        }

        private void AnimateTranslateY(TranslateTransform target, double toY, int durationMs)
        {
            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            var anim = new DoubleAnimation
            {
                To = toY,
                Duration = duration,
                EasingFunction = easing,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, "Y");

            storyboard.Children.Add(anim);
            storyboard.Begin();
        }

        private void AnimateTextColor(TextBlock target, Windows.UI.Color toColor, int durationMs)
        {
            // Ensure Foreground is an animatable SolidColorBrush
            if (target.Foreground is not SolidColorBrush brush)
            {
                brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 226, 225));
                target.Foreground = brush;
            }

            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            var anim = new ColorAnimation
            {
                To = toColor,
                Duration = duration,
                EasingFunction = easing,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(anim, brush);
            Storyboard.SetTargetProperty(anim, "Color");

            storyboard.Children.Add(anim);
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

        private async void RenameBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is Quill.Models.Book book)
            {
                var dialog = new ContentDialog
                {
                    Title = "Rename Book",
                    PrimaryButtonText = "Rename",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var inputBox = new TextBox
                {
                    PlaceholderText = "Enter new title",
                    Text = book.Title,
                    SelectionStart = 0,
                    SelectionLength = book.Title?.Length ?? 0
                };

                dialog.Content = inputBox;

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
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
                    return typedChild;

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}



//Check this out.
//< Border x: Name = "CardBackground" CornerRadius = "16" >
//                        < Border.Background >
//                            < AcrylicBrush TintColor = "#1B1B1C"
//                                          TintOpacity = "0.6"
//                                          TintLuminosityOpacity = "0.4"
//                                          FallbackColor = "#1B1B1C" />
//                        </ Border.Background >
//                    </ Border >