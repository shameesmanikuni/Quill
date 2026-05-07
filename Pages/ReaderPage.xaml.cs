using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Quill.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        public CancellationTokenSource? PageCts { get; set; }

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
        public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

        private ImageSource? _imageSrc;
        public ImageSource? ImageSrc
        {
            get => _imageSrc;
            set { _imageSrc = value; OnPropertyChanged(nameof(ImageSrc)); }
        }

        public bool HasRendered { get; set; } = false;
        public string TempFilePath { get; set; } = string.Empty;

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class ReaderPage : Page
    {
        public ObservableCollection<PdfPageItem> VirtualPages { get; } = new();

        private Book? _currentBook;
        private string _tempDir = string.Empty;
        private CancellationTokenSource? _cts;
        private int _targetCenterPage = 0;
        private CancellationTokenSource? _debounceCts;

        // NEW: Bypass the 150ms delay on initial load
        private bool _isFirstRender = true;

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

        // NEW: Synchronous save for when the user clicks the Windows "X" button
        public void ForceSaveProgress()
        {
            if (_currentBook != null)
            {
                _currentBook.LastReadDate = DateTime.Now;
                // .Wait() forces the app to hold off on dying until the JSON file is saved!
                Task.Run(async () => await Quill.Services.LibraryService.Instance.UpdateBookAsync(_currentBook)).Wait();
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            MainWindow.Instance?.SetReaderMode(false);

            // 1. Instantly stop all background rendering
            _debounceCts?.Cancel();
            _cts?.Cancel();

            if (_currentBook != null)
            {
                _currentBook.LastReadDate = DateTime.Now;
                await Quill.Services.LibraryService.Instance.UpdateBookAsync(_currentBook);
            }

            // 2. Break the image references
            foreach (var page in VirtualPages)
            {
                page.PageCts?.Cancel();
                if (page.ImageSrc is BitmapImage bmp)
                {
                    bmp.UriSource = null;
                }

                // This fires the event telling the UI to drop the image
                page.ImageSrc = null;
            }

            // ==========================================================
            // 3. CRITICAL PAUSE #1: 
            // We MUST wait 100ms here! This gives the WinUI layout engine 
            // enough time to process the 'null' events and physically 
            // unbind the DirectX surfaces from your graphics card.
            // ==========================================================
            await Task.Delay(100);

            // 4. Now that the images are dropped, clear the collections
            PageRepeater.ItemsSource = null;
            VirtualPages.Clear();

            // ==========================================================
            // 5. CRITICAL PAUSE #2: 
            // Give the ItemsRepeater 100ms to empty its internal C++ 
            // recycle pool before we completely destroy it.
            // ==========================================================
            await Task.Delay(100);

            // 6. Shred the UI tree
            if (this.Content is Grid rootGrid)
            {
                rootGrid.Children.Clear();
            }
            this.Content = null;

            // 7. Safely dispose MuPDF unmanaged memory
            await Task.Run(() =>
            {
                lock (_pdfLock)
                {
                    _pdfDoc?.Dispose();
                    _pdfDoc = null;
                }
            });

            // 8. The Scalpel: Flush the now-orphaned C# wrappers
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

            // 9. Delete temp files
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch { }
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

                    double defaultWidth = 800;
                    double defaultHeight = 1130;

                    dispatcher.TryEnqueue(async () =>
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

                        if (_currentBook != null && _currentBook.LastPageRead > 0 && _currentBook.LastPageRead < count)
                        {
                            PageIndicator.Text = $"Page {(_currentBook.LastPageRead) + 1} of {count}";
                            await Task.Delay(150);
                            double targetY = _currentBook.LastPageRead * (defaultHeight + 20);
                            ReaderScroll.ChangeView(null, targetY, null, true);
                        }
                        else
                        {
                            PageIndicator.Text = $"Page 1 of {count}";
                            UpdateRenderWindow(0);
                        }
                    });
                });
            }
            catch (Exception)
            {
                PageIndicator.Text = "Failed to open PDF.";
            }
        }

        private void ReaderScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (VirtualPages.Count == 0) return;

            double centerOfViewport = ReaderScroll.VerticalOffset + (ReaderScroll.ViewportHeight / 2);
            double pageHeight = VirtualPages[0].PageHeight + 20;

            int currentIndex = (int)(centerOfViewport / pageHeight);

            if (currentIndex < 0) currentIndex = 0;
            if (currentIndex >= VirtualPages.Count) currentIndex = VirtualPages.Count - 1;

            if (_targetCenterPage != currentIndex)
            {
                _targetCenterPage = currentIndex;

                if (_currentBook != null)
                {
                    _currentBook.LastPageRead = currentIndex;
                    PageIndicator.Text = $"Page {currentIndex + 1} of {VirtualPages.Count}";
                }

                TriggerDebouncedRender();
            }
        }

        private void PageRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            TriggerDebouncedRender();
        }

        private void PageRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            if (args.Element is Grid container)
            {
                foreach (var child in container.Children)
                {
                    if (child is Image img) img.Source = null;
                }
            }
        }

        private async void TriggerDebouncedRender()
        {
            // FIX 1: Instant rendering when the book first opens
            if (_isFirstRender)
            {
                _isFirstRender = false;
                UpdateRenderWindow(_targetCenterPage);
                return;
            }

            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            try
            {
                await Task.Delay(150, token);
                if (!token.IsCancellationRequested) UpdateRenderWindow(_targetCenterPage);
            }
            catch { }
        }

        private void UpdateRenderWindow(int centerPage)
        {
            int totalPages = VirtualPages.Count;
            int renderStart = Math.Max(0, centerPage - 2);
            int renderEnd = Math.Min(totalPages - 1, centerPage + 2);
            int keepStart = Math.Max(0, centerPage - 3);
            int keepEnd = Math.Min(totalPages - 1, centerPage + 3);

            // 1. Unload off-screen pages instantly
            for (int i = 0; i < totalPages; i++)
            {
                if (i < keepStart || i > keepEnd)
                {
                    var page = VirtualPages[i];
                    if (page.HasRendered || page.IsLoading)
                    {
                        page.PageCts?.Cancel();
                        page.PageCts = null;
                        if (page.ImageSrc is BitmapImage bmp) bmp.UriSource = null;
                        page.ImageSrc = null;
                        page.HasRendered = false;
                        page.IsLoading = false;
                    }
                }
            }

            // FIX 2: PRIORITY SORTING. Build the render queue based on distance to the center page.
            var renderQueue = new List<int>();
            for (int i = renderStart; i <= renderEnd; i++) renderQueue.Add(i);

            // This ensures the page you are staring at renders FIRST, then the ones above/below it
            renderQueue = renderQueue.OrderBy(i => Math.Abs(i - centerPage)).ToList();

            foreach (int i in renderQueue)
            {
                var page = VirtualPages[i];
                if (!page.HasRendered && !page.IsLoading)
                {
                    page.PageCts = new CancellationTokenSource();
                    _ = RenderSpecificPageAsync(page, page.PageCts.Token);
                }
            }
        }

        private async Task RenderSpecificPageAsync(PdfPageItem item, CancellationToken pageToken)
        {
            item.IsLoading = true;
            var globalToken = _cts?.Token ?? CancellationToken.None;
            var dispatcher = this.DispatcherQueue;

            await Task.Run(() =>
            {
                try
                {
                    if (globalToken.IsCancellationRequested || pageToken.IsCancellationRequested) return;

                    if (!File.Exists(item.TempFilePath))
                    {
                        lock (_pdfLock)
                        {
                            if (globalToken.IsCancellationRequested || pageToken.IsCancellationRequested || _pdfDoc == null) return;
                            using MuPDF.NET.Page pdfPage = _pdfDoc[item.PageNumber];

                            // FIX 3: Matrix 1.5f produces images 64% smaller than 2.5f!
                            // It eliminates the Disk I/O bottleneck while keeping text sharp.
                            MuPDF.NET.Matrix matrix = new MuPDF.NET.Matrix(1.5f, 1.5f);
                            using MuPDF.NET.Pixmap pixmap = pdfPage.GetPixmap(matrix);
                            pixmap.Save(item.TempFilePath);
                        }
                    }

                    if (!globalToken.IsCancellationRequested && !pageToken.IsCancellationRequested)
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            if (pageToken.IsCancellationRequested) return;

                            string uriPath = $"file:///{item.TempFilePath.Replace('\\', '/')}";
                            var bitmap = new BitmapImage();
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                            bitmap.UriSource = new Uri(uriPath);

                            item.ImageSrc = bitmap;
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}