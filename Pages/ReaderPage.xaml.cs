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
    // Wrapper class for virtualization
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
            set { _isLoading = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading))); }
        }

        private ImageSource? _imageSrc;
        public ImageSource? ImageSrc
        {
            get => _imageSrc;
            set { _imageSrc = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageSrc))); }
        }

        public bool HasRendered { get; set; } = false;
        public string TempFilePath { get; set; } = string.Empty;
    }

    public sealed partial class ReaderPage : Page
    {
        public ObservableCollection<PdfPageItem> VirtualPages { get; } = new();

        private Book? _currentBook;
        private string _tempDir = string.Empty;
        private CancellationTokenSource? _cts;

        // Single PDF Document reference & lock
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
            MainWindow.Instance?.SetReaderMode(true);

            if (e.Parameter is Book book)
            {
                _currentBook = book;
                BookTitleLabel.Text = book.Title;
                await InitializePdfAsync(book.FilePath);
            }
        }

        private async Task InitializePdfAsync(string filePath)
        {
            _cts = new CancellationTokenSource();
            _tempDir = Path.Combine(Path.GetTempPath(), "QuillReader", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            // FIX: Capture the UI dispatcher BEFORE going to the background thread
            var dispatcher = this.DispatcherQueue;

            try
            {
                PageIndicator.Text = "Opening book...";

                await Task.Run(() =>
                {
                    _pdfDoc = new MuPDF.NET.Document(filePath);
                    int count = _pdfDoc.PageCount;

                    double defaultWidth = 800;
                    double defaultHeight = 1130;

                    // FIX: Use the captured dispatcher
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
                        PageIndicator.Text = $"{count} Pages";
                    });
                });
            }
            catch (Exception ex)
            {
                PageIndicator.Text = "Failed to open PDF.";
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        // Triggered automatically when you scroll and the dummy Grid becomes visible
        // Triggered automatically by the ItemsRepeater right before a page scrolls into view
        private void PageRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            // args.Index guarantees we get the exact page number being shown on screen right now!
            if (args.Index >= 0 && args.Index < VirtualPages.Count)
            {
                UpdateRenderWindow(args.Index);
            }
        }

        // --- NEW: Sliding Window Memory Management ---
        private void UpdateRenderWindow(int centerPage)
        {
            int totalPages = VirtualPages.Count;

            // Render Window: 2 behind, current, 2 in front (Total 5)
            int renderStart = Math.Max(0, centerPage - 2);
            int renderEnd = Math.Min(totalPages - 1, centerPage + 2);

            // Keep-Alive Buffer: Give it a slightly wider buffer (+/- 3) for unloading RAM
            // to prevent the image from flickering if you slowly scroll up and down by 1 page.
            int keepStart = Math.Max(0, centerPage - 3);
            int keepEnd = Math.Min(totalPages - 1, centerPage + 3);

            for (int i = 0; i < totalPages; i++)
            {
                var page = VirtualPages[i];

                // 1. Render pages in the 5-page window
                if (i >= renderStart && i <= renderEnd)
                {
                    if (!page.HasRendered && !page.IsLoading)
                    {
                        _ = RenderSpecificPageAsync(page);
                    }
                }
                // 2. Unload pages strictly outside the keep-alive window
                else if (i < keepStart || i > keepEnd)
                {
                    if (page.HasRendered)
                    {
                        // Instantly frees the RAM! 
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

            // FIX: Capture the UI dispatcher BEFORE going to the background thread
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
                        // FIX: Use the captured dispatcher
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
                    // FIX: Use the captured dispatcher
                    dispatcher.TryEnqueue(() => item.IsLoading = false);
                }
            });
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Instantly kill all queued rendering tasks
            _cts?.Cancel();

            // 2. Clear UI memory
            VirtualPages.Clear();
            PageRepeater.ItemsSource = null;

            // 3. Free MuPDF memory
            _pdfDoc?.Dispose();

            // 4. Delete the huge temporary files folder from your disk
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { /* Ignore file lock exceptions if a thread was mid-save */ }

            // 5. Restore App UI and go back
            MainWindow.Instance?.SetReaderMode(false);
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}