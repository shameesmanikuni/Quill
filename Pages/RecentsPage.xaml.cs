using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Quill.ViewModels;
using System;

namespace Quill.Pages
{
    public sealed partial class RecentsPage : Page
    {
        public RecentsViewModel ViewModel { get; } = new RecentsViewModel();

        public RecentsPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Clear the forward stack exactly like the LibraryPage to kill ghost Reader pages
            this.Frame.ForwardStack.Clear();

            try
            {
                await ViewModel.LoadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load recents: {ex.Message}");
            }
        }

        private void BookCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Quill.Models.Book book)
            {
                // 1. Removed the Task.Delay(50) so it fires the exact millisecond you click

                // 2. Added SuppressNavigationTransitionInfo() to kill the slide-up animation
                this.Frame.Navigate(
                    typeof(Quill.Pages.ReaderPage),
                    book,
                    new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo()
                );
            }
        }
    }
}