using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Quill.Models
{
    public class Book : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _title = string.Empty;
        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }

        public string Author { get; set; } = "Unknown Author";
        public string FilePath { get; set; } = string.Empty;

        private string _coverPath = string.Empty;
        public string CoverPath
        {
            get => _coverPath;
            set
            {
                _coverPath = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CoverImage));
                OnPropertyChanged(nameof(CoverVisibility));
                OnPropertyChanged(nameof(PlaceholderVisibility));
            }
        }

        // 1. Create a private variable to hold the image in memory
        private BitmapImage? _coverBitmap;

        [JsonIgnore]
        public ImageSource? CoverImage
        {
            get
            {
                if (!HasValidCover) return null;

                // 2. Only create the image if we haven't already!
                if (_coverBitmap == null)
                {
                    try
                    {
                        string uriPath = $"file:///{CoverPath.Replace('\\', '/')}";
                        _coverBitmap = new BitmapImage();
                        _coverBitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        _coverBitmap.UriSource = new Uri(uriPath);
                    }
                    catch { return null; }
                }

                // 3. Return the exact same image every time WinUI asks for it
                return _coverBitmap;
            }
        }

        // NEW: Validates that the file path isn't just set, but physically exists on the hard drive
        [JsonIgnore]
        public bool HasValidCover => !string.IsNullOrWhiteSpace(CoverPath) && System.IO.File.Exists(CoverPath);

        [JsonIgnore]
        public Visibility CoverVisibility => HasValidCover ? Visibility.Visible : Visibility.Collapsed;

        [JsonIgnore]
        public Visibility PlaceholderVisibility => HasValidCover ? Visibility.Collapsed : Visibility.Visible;

        public string Format { get; set; } = "PDF";
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        private double _lastPageRead = 0;
        public double LastPageRead { get => _lastPageRead; set { _lastPageRead = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReadingProgressText)); OnPropertyChanged(nameof(PageProgressText)); } }

        private double _totalPages = 0;
        public double TotalPages { get => _totalPages; set { _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTotalPages)); OnPropertyChanged(nameof(ReadingProgressText)); OnPropertyChanged(nameof(PageProgressText)); } }

        public DateTime? LastReadDate { get; set; } = null;

        [JsonIgnore]
        public double DisplayTotalPages => Math.Max(TotalPages, 1.0);

        [JsonIgnore]
        public string ReadingProgressText => TotalPages > 0 ? $"{(int)((LastPageRead / TotalPages) * 100)}% completed" : "Not started";

        [JsonIgnore]
        public string PageProgressText => TotalPages > 0 ? $"Page {(int)LastPageRead} of {(int)TotalPages}" : string.Empty;

        [JsonIgnore]
        public bool HasBeenRead => LastReadDate.HasValue || LastPageRead > 0;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}