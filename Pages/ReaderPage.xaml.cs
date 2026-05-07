using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Quill.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Quill.Pages
{
    public sealed partial class ReaderPage : Page
    {
        public ObservableCollection<ImageSource> RenderedPages { get; } = new();
        private Book? _currentBook;
        private CancellationTokenSource? _cts; // Token to cancel rendering

        public ReaderPage()
        {
            this.InitializeComponent();
            PageRepeater.ItemsSource = RenderedPages;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Hide the global Sidebar and Topbar completely
            MainWindow.Instance?.SetReaderMode(true);

            if (e.Parameter is Book book)
            {
                _currentBook = book;
                BookTitleLabel.Text = book.Title;
                await LoadPdfAsync(book.FilePath);
            }
        }

        private async Task LoadPdfAsync(string filePath)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            try
            {
                dispatcher.TryEnqueue(() => PageIndicator.Text = "Loading PDF...");

                string tempDir = Path.Combine(Path.GetTempPath(), "QuillReader", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                await Task.Run(() =>
                {
                    using MuPDF.NET.Document doc = new MuPDF.NET.Document(filePath);
                    int count = doc.PageCount;

                    for (int i = 0; i < count; i++)
                    {
                        // INSTANT ABORT: Check if user pressed Back Button
                        if (token.IsCancellationRequested) break;

                        using MuPDF.NET.Page pdfPage = doc[i];

                        // FIX FOR CLARITY: Apply a 3x scale matrix for super crisp HD rendering
                        MuPDF.NET.Matrix matrix = new MuPDF.NET.Matrix(3f, 3f);
                        using MuPDF.NET.Pixmap pixmap = pdfPage.GetPixmap(matrix);

                        string tempFile = Path.Combine(tempDir, $"page_{i}.png");
                        pixmap.Save(tempFile);

                        // Double check token before updating UI
                        if (token.IsCancellationRequested) break;

                        dispatcher.TryEnqueue(() =>
                        {
                            try
                            {
                                string uriPath = $"file:///{tempFile.Replace('\\', '/')}";
                                var bitmap = new BitmapImage(new Uri(uriPath));

                                RenderedPages.Add(bitmap);
                                PageIndicator.Text = $"Loaded {i + 1} of {count}";
                            }
                            catch (Exception imgEx)
                            {
                                PageIndicator.Text = $"Image Error: {imgEx.Message}";
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                dispatcher.TryEnqueue(() => PageIndicator.Text = $"PDF Error: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Immediately kill the background rendering task
            _cts?.Cancel();

            // 2. Bring back the global Sidebar and Topbar
            MainWindow.Instance?.SetReaderMode(false);

            // 3. Go back to Library
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}