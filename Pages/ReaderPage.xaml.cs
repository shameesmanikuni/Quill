using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Quill.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Quill.Pages
{
    public class PdfPageItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int PageNumber { get; set; }
        public double PageWidth { get; set; }
        public double PageHeight { get; set; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(LoadingVisibility));
            }
        }

        public Visibility LoadingVisibility =>
            IsLoading ? Visibility.Visible : Visibility.Collapsed;

        private ImageSource? _imageSrc;
        public ImageSource? ImageSrc
        {
            get => _imageSrc;
            set { _imageSrc = value; OnPropertyChanged(nameof(ImageSrc)); }
        }

        public bool HasRendered { get; set; } = false;
        public string TempFilePath { get; set; } = string.Empty;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class ReaderPage : Page
    {
        public ObservableCollection<PdfPageItem> VirtualPages { get; } = new();

        private Book? _currentBook;
        private string _tempDir = string.Empty;
        private CancellationTokenSource? _cts;

        private MuPDF.NET.Document? _pdfDoc;
        private readonly object _pdfLock = new object();

        public ReaderPage()
        {
            this.InitializeComponent();
            PageRepeater.ItemsSource = VirtualPages;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Enter Immersive Mode (Ensure you updated MainWindow with the SetReaderMode method)
            MainWindow.Instance?.SetReaderMode(true);

            if (e.Parameter is Book book)
            {
                _currentBook = book;
                BookTitleLabel.Text = book.Title;

                await InitializePdfAsync(book.FilePath);

                // FIX: Auto-jump to the last saved page using StartBringIntoView
                if (_currentBook.LastPageRead > 0 && _currentBook.LastPageRead < VirtualPages.Count)
                {
                    // A small delay ensures the repeater has laid out its basic structure
                    await Task.Delay(200);

                    // The correct way to scroll an ItemsRepeater to an index:
                    var element = PageRepeater.GetOrCreateElement((int)_currentBook.LastPageRead);
                    element.StartBringIntoView();
                }
            }
        }

        private async Task InitializePdfAsync(string filePath)
        {
            _cts = new CancellationTokenSource();
            _tempDir = Path.Combine(Path.GetTempPath(), "QuillReader", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            var dispatcher = this.DispatcherQueue;

            try
            {
                PageIndicator.Text = "Opening book...";

                await Task.Run(() =>
                {
                    _pdfDoc = new MuPDF.NET.Document(filePath);
                    int count = _pdfDoc.PageCount;

                    // Standard aspect ratio placeholders
                    double defaultWidth = 800;
                    double defaultHeight = 1130;

                    dispatcher.TryEnqueue(() =>
                    {
                        for (int i = 0; i < count; i++)
                        {
                            VirtualPages.Add(new PdfPageItem
                            {
                                PageNumber = i,
                                PageWidth = defaultWidth,
                                PageHeight = defaultHeight,
                                TempFilePath = Path.Combine(_tempDir, $"page_{i}.png")
                            });
                        }
                        // Initial indicator setup
                        PageIndicator.Text = $"Page {(_currentBook?.LastPageRead ?? 0) + 1} of {count}";
                    });
                });
            }
            catch (Exception ex)
            {
                PageIndicator.Text = "Failed to open PDF.";
                System.Diagnostics.Debug.WriteLine($"PDF Init Error: {ex.Message}");
            }
        }

        private void PageRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Index >= 0 && args.Index < VirtualPages.Count)
            {
                UpdateRenderWindow(args.Index);

                // Update the Book model in real-time as the user scrolls
                if (_currentBook != null)
                {
                    _currentBook.LastPageRead = args.Index;
                    // Mark progress date so it moves to the front of "Continue Reading"
                    _currentBook.LastReadDate = DateTime.Now;

                    PageIndicator.Text = $"Page {args.Index + 1} of {VirtualPages.Count}";
                }
            }
        }

        private void UpdateRenderWindow(int centerPage)
        {
            int totalPages = VirtualPages.Count;
            int renderStart = Math.Max(0, centerPage - 2);
            int renderEnd = Math.Min(totalPages - 1, centerPage + 2);
            int keepStart = Math.Max(0, centerPage - 3);
            int keepEnd = Math.Min(totalPages - 1, centerPage + 3);

            for (int i = 0; i < totalPages; i++)
            {
                var page = VirtualPages[i];
                if (i >= renderStart && i <= renderEnd)
                {
                    if (!page.HasRendered && !page.IsLoading)
                        _ = RenderSpecificPageAsync(page);
                }
                else if (i < keepStart || i > keepEnd)
                {
                    if (page.HasRendered)
                    {
                        page.ImageSrc = null;
                        page.HasRendered = false;
                        page.IsLoading = false;
                    }
                }
            }
        }

        private async Task RenderSpecificPageAsync(PdfPageItem item)
        {
            item.IsLoading = true;
            var token = _cts?.Token ?? CancellationToken.None;
            var dispatcher = this.DispatcherQueue;

            await Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    if (!File.Exists(item.TempFilePath))
                    {
                        lock (_pdfLock)
                        {
                            if (token.IsCancellationRequested || _pdfDoc == null) return;
                            using MuPDF.NET.Page pdfPage = _pdfDoc[item.PageNumber];
                            MuPDF.NET.Matrix matrix = new MuPDF.NET.Matrix(2.5f, 2.5f);
                            using MuPDF.NET.Pixmap pixmap = pdfPage.GetPixmap(matrix);
                            pixmap.Save(item.TempFilePath);
                        }
                    }

                    if (!token.IsCancellationRequested)
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            string uriPath = $"file:///{item.TempFilePath.Replace('\\', '/')}";
                            item.ImageSrc = new BitmapImage(new Uri(uriPath));
                            item.HasRendered = true;
                            item.IsLoading = false;
                        });
                    }
                }
                catch
                {
                    dispatcher.TryEnqueue(() => item.IsLoading = false);
                }
            });
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Immediately kill background rendering
            _cts?.Cancel();

            // 2. Persist the current progress to the service before navigating away
            if (_currentBook != null)
            {
                _currentBook.LastReadDate = DateTime.Now;
                await Quill.Services.LibraryService.Instance.UpdateBookAsync(_currentBook);
            }

            // 3. Free MuPDF resources
            _pdfDoc?.Dispose();
            _pdfDoc = null;

            // 4. Cleanup temporary image files
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { /* Ignore locks if thread was finishing a save */ }

            // 5. Restore Side/Top bars and navigate back
            MainWindow.Instance?.SetReaderMode(false);
            if (Frame.CanGoBack)
                Frame.GoBack();
        }
    }
}