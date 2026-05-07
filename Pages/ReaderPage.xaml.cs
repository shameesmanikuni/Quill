using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Quill.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace Quill.Pages
{
    public sealed partial class ReaderPage : Page // This 'Page' stays as the WinUI Page
    {
        public ObservableCollection<ImageSource> RenderedPages { get; } = new();
        private Book? _currentBook;

        public ReaderPage()
        {
            this.InitializeComponent();
            PageRepeater.ItemsSource = RenderedPages;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Book book)
            {
                _currentBook = book;
                BookTitleLabel.Text = book.Title;
                await LoadPdfAsync(book.FilePath);
            }
        }

        private async Task LoadPdfAsync(string filePath)
        {
            // Capture the UI thread so background tasks can talk to the screen safely
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            try
            {
                dispatcher.TryEnqueue(() => PageIndicator.Text = "Loading PDF...");

                // Create a temporary cache directory
                string tempDir = Path.Combine(Path.GetTempPath(), "QuillReader", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                await Task.Run(() =>
                {
                    using MuPDF.NET.Document doc = new MuPDF.NET.Document(filePath);
                    int count = doc.PageCount;

                    for (int i = 0; i < count; i++)
                    {
                        using MuPDF.NET.Page pdfPage = doc[i];

                        // We will remove the Matrix scaling entirely for this test to 
                        // guarantee we don't have any MuPDF API version conflicts.
                        using MuPDF.NET.Pixmap pixmap = pdfPage.GetPixmap();

                        string tempFile = Path.Combine(tempDir, $"page_{i}.png");
                        pixmap.Save(tempFile);

                        dispatcher.TryEnqueue(() =>
                        {
                            try
                            {
                                // FIX: WinUI 3 requires local paths to be formatted as strict URIs
                                string uriPath = $"file:///{tempFile.Replace('\\', '/')}";
                                var bitmap = new BitmapImage(new Uri(uriPath));

                                RenderedPages.Add(bitmap);
                                PageIndicator.Text = $"Loaded {i + 1} of {count}";
                            }
                            catch (Exception imgEx)
                            {
                                // If WinUI fails to load the image, show it on screen
                                PageIndicator.Text = $"Image Error: {imgEx.Message}";
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                // If MuPDF fails to parse the file, show it on screen
                dispatcher.TryEnqueue(() => PageIndicator.Text = $"PDF Error: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}