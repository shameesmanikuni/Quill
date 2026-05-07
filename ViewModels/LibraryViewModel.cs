using Quill.Models;
using Quill.Services;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Quill.ViewModels
{
    public sealed class LibraryViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Collections ──────────────────────────────────────────────────────

        private ObservableCollection<Book> _allBooks = new();
        private ObservableCollection<Book> _recentBooks = new();

        public ObservableCollection<Book> AllBooks
        {
            get => _allBooks;
            private set
            {
                _allBooks = value;
                OnPropertyChanged();
                NotifyDerivedProperties();
            }
        }

        public ObservableCollection<Book> RecentBooks
        {
            get => _recentBooks;
            private set
            {
                _recentBooks = value;
                OnPropertyChanged();
                NotifyDerivedProperties();
            }
        }

        // ── Loading state ─────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        // ── Derived – boolean ─────────────────────────────────────────────────

        public bool HasBooks => _allBooks.Count > 0;
        public bool HasRecentBooks => _recentBooks.Count > 0;

        /// <summary>True when there are ≥ 3 recent books (arrows are shown).</summary>
        public bool ShowContinueReadingArrows => _recentBooks.Count >= 3;

        // ── Derived – Visibility (for x:Bind in XAML) ─────────────────────────

        public Visibility BooksGridVisibility => ToVis(HasBooks);
        public Visibility EmptyBooksVisibility => ToVis(!HasBooks);
        public Visibility RecentBooksVisibility => ToVis(HasRecentBooks);
        public Visibility EmptyRecentBooksVisibility => ToVis(!HasRecentBooks);
        public Visibility ArrowsVisibility => ToVis(ShowContinueReadingArrows);

        // ── Public methods ────────────────────────────────────────────────────

        /// <summary>
        /// First-time load: initialises the service then populates collections.
        /// </summary>
        public async Task LoadAsync()
        {
            IsLoading = true;
            await LibraryService.Instance.InitializeAsync();
            await RefreshCollectionsAsync();
            IsLoading = false;
        }

        /// <summary>
        /// Re-reads from the service (call after an import or delete).
        /// </summary>
        public async Task RefreshAsync()
        {
            await RefreshCollectionsAsync();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task RefreshCollectionsAsync()
        {
            var uiDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            await System.Threading.Tasks.Task.Run(() =>
            {
                var all = LibraryService.Instance.GetAllBooks();

                // FIX: Change GetRecentBooks(10) to GetRecentBooks(3)
                var recent = LibraryService.Instance.GetRecentBooks(3);

                uiDispatcher?.TryEnqueue(() =>
                {
                    AllBooks = new ObservableCollection<Book>(all);
                    RecentBooks = new ObservableCollection<Book>(recent);
                });
            });
        }

        private void NotifyDerivedProperties()
        {
            OnPropertyChanged(nameof(HasBooks));
            OnPropertyChanged(nameof(HasRecentBooks));
            OnPropertyChanged(nameof(ShowContinueReadingArrows));
            OnPropertyChanged(nameof(BooksGridVisibility));
            OnPropertyChanged(nameof(EmptyBooksVisibility));
            OnPropertyChanged(nameof(RecentBooksVisibility));
            OnPropertyChanged(nameof(EmptyRecentBooksVisibility));
            OnPropertyChanged(nameof(ArrowsVisibility));
        }

        private static Visibility ToVis(bool value)
            => value ? Visibility.Visible : Visibility.Collapsed;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}