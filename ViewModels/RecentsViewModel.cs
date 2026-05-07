using Quill.Models;
using Quill.Services;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Quill.ViewModels
{
    public sealed class RecentsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<Book> _recentBooks = new();
        public ObservableCollection<Book> RecentBooks
        {
            get => _recentBooks;
            private set
            {
                _recentBooks = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasBooks));
                OnPropertyChanged(nameof(GridVisibility));
                OnPropertyChanged(nameof(EmptyVisibility));
            }
        }

        public bool HasBooks => _recentBooks.Count > 0;
        public Visibility GridVisibility => HasBooks ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EmptyVisibility => !HasBooks ? Visibility.Visible : Visibility.Collapsed;

        public async Task LoadAsync()
        {
            var uiDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            await Task.Run(() =>
            {
                // Grab all books that have been read, naturally sorted by LastReadDate
                var recents = LibraryService.Instance.GetRecentBooks(9999);

                uiDispatcher?.TryEnqueue(() =>
                {
                    RecentBooks = new ObservableCollection<Book>(recents);
                });
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}