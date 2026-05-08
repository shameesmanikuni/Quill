using Quill.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using MuPDF.NET; // Correct Artifex MuPDF.NET namespace

namespace Quill.Services
{
    // Custom exception to be caught by the UI for showing the toast message
    public class DuplicateBookException : Exception
    {
        public DuplicateBookException(string message) : base(message) { }
    }

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
            return Path.Combine(localAppData, "Quill");
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

        // Limit changed to 3 as requested for the Continue Reading section
        // Update this method in LibraryService.cs
        public List<Book> GetRecentBooks(int count = 3)
        {
            return _books
                   .Where(b => b.HasBeenRead)
                   // Sort by LastReadDate descending so the newest is always index 0 (left-most)
                   .OrderByDescending(b => b.LastReadDate ?? DateTime.MinValue)
                   .Take(count)
                   .ToList();
        }

        public async Task RenameBookAsync(string bookId, string newTitle)
        {
            // Find the book in the current in-memory list
            var book = _books.FirstOrDefault(b => b.Id == bookId);

            if (book != null)
            {
                // Update the title
                book.Title = newTitle;

                // Save the updated list back to the JSON database
                await SaveBooksAsync();
            }
        }

        //public async Task<Book?> ImportBookAsync(nint windowHandle)
        //{
        //    EnsureInitialized();

        //    // 1. Open file picker
        //    var picker = new FileOpenPicker
        //    {
        //        ViewMode = PickerViewMode.Thumbnail,
        //        SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        //    };
        //    picker.FileTypeFilter.Add(".pdf");
        //    picker.FileTypeFilter.Add(".epub");

        //    WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

        //    var file = await picker.PickSingleFileAsync();
        //    if (file is null || string.IsNullOrEmpty(file.Path)) return null;

        //    var title = Path.GetFileNameWithoutExtension(file.Name);

        //    // 2. Duplicate Check
        //    if (_books.Any(b => string.Equals(b.Title, title, StringComparison.OrdinalIgnoreCase)))
        //    {
        //        throw new DuplicateBookException($"'{title}' is already in your library.");
        //    }

        //    // 3. Setup standard IO paths
        //    var bookId = Guid.NewGuid().ToString();
        //    string bookFolder = Path.Combine(GetAppBasePath(), LibraryFolderName, bookId);
        //    Directory.CreateDirectory(bookFolder);

        //    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        //    var destFilePath = Path.Combine(bookFolder, "book" + ext);

        //    int totalPages = 0;
        //    string coverPath = string.Empty;

        //    // 4. Copy file and parse PDF on a background thread so the UI doesn't freeze
        //    await Task.Run(() =>
        //    {
        //        File.Copy(file.Path, destFilePath, overwrite: true);

        //        if (ext == ".pdf")
        //        {
        //            try
        //            {
        //                // Official MuPDF.NET API (Abstracts Context away entirely)
        //                using Document doc = new Document(destFilePath);

        //                totalPages = doc.PageCount;

        //                if (totalPages > 0)
        //                {
        //                    Page page = doc[0]; // Zero-based index for the first page

        //                    // GetPixmap without arguments defaults to 72 DPI. 
        //                    // This creates an image roughly 612x792 pixels, which is perfect for a cover thumbnail.
        //                    Pixmap pixmap = page.GetPixmap();

        //                    string coverFile = Path.Combine(bookFolder, "cover.png");
        //                    pixmap.Save(coverFile);
        //                    coverPath = coverFile;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                // If cover extraction fails, it will gracefully fallback to the gradient UI
        //                System.Diagnostics.Debug.WriteLine($"MuPDF Cover extraction failed: {ex.Message}");
        //            }
        //        }
        //    });

        //    // 5. Build Book record
        //    var book = new Book
        //    {
        //        Id = bookId,
        //        Title = title,
        //        Author = "Unknown Author",
        //        FilePath = destFilePath,
        //        CoverPath = coverPath,
        //        TotalPages = totalPages,
        //        Format = ext.TrimStart('.').ToUpperInvariant(),
        //        DateAdded = DateTime.UtcNow,
        //    };

        //    _books.Add(book);
        //    await SaveBooksAsync();
        //    return book;
        //}

        public async Task<List<Book>> ImportBooksAsync(nint windowHandle)
        {
            EnsureInitialized();

            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };

            // Restricted to PDF only
            picker.FileTypeFilter.Clear();
            picker.FileTypeFilter.Add(".pdf");

            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

            // Multiselect enabled here
            var files = await picker.PickMultipleFilesAsync();
            if (files is null || files.Count == 0) return new List<Book>();

            var importedBooks = new List<Book>();
            var duplicates = new List<string>();

            foreach (var file in files)
            {
                var title = Path.GetFileNameWithoutExtension(file.Name);

                // Check for duplicates and set them aside
                if (_books.Any(b => string.Equals(b.Title, title, StringComparison.OrdinalIgnoreCase)))
                {
                    duplicates.Add(title);
                    continue; // Skip this one, but keep processing the rest
                }

                var bookId = Guid.NewGuid().ToString();
                string bookFolder = Path.Combine(GetAppBasePath(), LibraryFolderName, bookId);
                Directory.CreateDirectory(bookFolder);

                var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                var destFilePath = Path.Combine(bookFolder, "book" + ext);

                int totalPages = 0;
                string coverPath = string.Empty;

                await Task.Run(() =>
                {
                    File.Copy(file.Path, destFilePath, overwrite: true);

                    try
                    {
                        using Document doc = new Document(destFilePath);
                        totalPages = doc.PageCount;

                        if (totalPages > 0)
                        {
                            Page page = doc[0];
                            Pixmap pixmap = page.GetPixmap();
                            string coverFile = Path.Combine(bookFolder, "cover.png");
                            pixmap.Save(coverFile);
                            coverPath = coverFile;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MuPDF Cover extraction failed: {ex.Message}");
                    }
                });

                var book = new Book
                {
                    Id = bookId,
                    Title = title,
                    Author = "Unknown Author",
                    FilePath = destFilePath,
                    CoverPath = coverPath,
                    TotalPages = totalPages,
                    Format = ext.TrimStart('.').ToUpperInvariant(),
                    DateAdded = DateTime.UtcNow,
                };

                _books.Add(book);
                importedBooks.Add(book);
            }

            if (importedBooks.Count > 0)
            {
                await SaveBooksAsync();
            }

            // If we found any duplicates in the batch, throw the exception to trigger the Toast in the UI
            if (duplicates.Count > 0)
            {
                string names = string.Join(", ", duplicates);
                string msg = duplicates.Count == 1
                    ? $"'{names}' is already in your library"
                    : $"These books were skipped (already in library): {names}";
                throw new DuplicateBookException(msg);
            }

            return importedBooks;
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