using EtherealPDF.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace EtherealPDF.Services
{
    public sealed class LibraryService
    {
        private static LibraryService? _instance;
        public static LibraryService Instance => _instance ??= new LibraryService();
        private LibraryService() { }

        private const string LibraryFolderName = "Library";
        private const string DatabaseFileName = "books.json";

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        private List<Book> _books = new();
        private bool _initialized = false;

        // ── Standard .NET Pathing ───────────────────────────────────────────

        private string GetAppBasePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "EtherealPDF");
        }

        // ── Public API ───────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            string baseDir = GetAppBasePath();
            string libraryDir = Path.Combine(baseDir, LibraryFolderName);

            // System.IO creates the folders safely and instantly
            Directory.CreateDirectory(baseDir);
            Directory.CreateDirectory(libraryDir);

            await LoadBooksAsync();
            _initialized = true;
        }

        public List<Book> GetAllBooks() => _books.OrderByDescending(b => b.DateAdded).ToList();

        public List<Book> GetRecentBooks(int count = 10) => _books
               .Where(b => b.HasBeenRead)
               .OrderByDescending(b => b.LastReadDate ?? b.DateAdded)
               .Take(count)
               .ToList();

        public async Task<Book?> ImportBookAsync(nint windowHandle)
        {
            EnsureInitialized();

            // 1. Open file picker
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".epub");

            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

            var file = await picker.PickSingleFileAsync();
            if (file is null || string.IsNullOrEmpty(file.Path)) return null;

            // 2. Setup standard IO paths
            var bookId = Guid.NewGuid().ToString();
            string bookFolder = Path.Combine(GetAppBasePath(), LibraryFolderName, bookId);
            Directory.CreateDirectory(bookFolder);

            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            var destFilePath = Path.Combine(bookFolder, "book" + ext);

            // 3. Copy file on a background thread so the UI doesn't freeze
            await Task.Run(() =>
            {
                File.Copy(file.Path, destFilePath, overwrite: true);
            });

            // 4. Build Book record
            var book = new Book
            {
                Id = bookId,
                Title = Path.GetFileNameWithoutExtension(file.Name),
                Author = "Unknown Author",
                FilePath = destFilePath,
                Format = ext.TrimStart('.').ToUpperInvariant(),
                DateAdded = DateTime.UtcNow,
            };

            _books.Add(book);
            await SaveBooksAsync();
            return book;
        }

        public async Task UpdateBookAsync(Book updated)
        {
            var i = _books.FindIndex(b => b.Id == updated.Id);
            if (i < 0) return;
            _books[i] = updated;
            await SaveBooksAsync();
        }

        public async Task DeleteBookAsync(string bookId)
        {
            var book = _books.FirstOrDefault(b => b.Id == bookId);
            if (book is null) return;

            string bookFolder = Path.Combine(GetAppBasePath(), LibraryFolderName, bookId);

            if (Directory.Exists(bookFolder))
            {
                Directory.Delete(bookFolder, recursive: true);
            }

            _books.Remove(book);
            await SaveBooksAsync();
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private void EnsureInitialized()
        {
            if (!_initialized) throw new InvalidOperationException("LibraryService.InitializeAsync() must be awaited before use.");
        }

        private async Task LoadBooksAsync()
        {
            string dbPath = Path.Combine(GetAppBasePath(), DatabaseFileName);
            if (File.Exists(dbPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(dbPath);
                    _books = JsonSerializer.Deserialize<List<Book>>(json, JsonOpts) ?? new List<Book>();
                }
                catch
                {
                    // If the file is corrupted from the previous crashes, ignore it and start fresh!
                    _books = new List<Book>();
                }
            }
        }

        private async Task SaveBooksAsync()
        {
            string dbPath = Path.Combine(GetAppBasePath(), DatabaseFileName);
            var json = JsonSerializer.Serialize(_books, JsonOpts);
            await File.WriteAllTextAsync(dbPath, json);
        }
    }
}