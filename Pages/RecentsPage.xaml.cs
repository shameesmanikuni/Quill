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

        private async void BookCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Quill.Models.Book book)
            {
                // Smooth click animation delay
                await System.Threading.Tasks.Task.Delay(0);

                this.Frame.Navigate(typeof(Quill.Pages.ReaderPage), book);
            }
        }
    }
}