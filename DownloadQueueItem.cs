using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace get_link_manga
{
    public class ErrorDetail
    {
        public string ChapterName { get; set; }
        public int PageNumber { get; set; }
        public string ErrorMessage { get; set; }
        public string ImageUrl { get; set; }

        public override string ToString()
        {
            return $"{ChapterName}, Trang {PageNumber} — {ErrorMessage}";
        }
    }

    public class DownloadQueueItem : INotifyPropertyChanged
    {
        private string _name;
        private string _sourceUrl;
        private string _sourceDomain;
        private int _totalChapters;
        private int _completedChapters;
        private string _status;
        private string _currentProcess;
        private int _errorCount;
        private string _downloadPath;
        private double _progressPercent;
        private List<ErrorDetail> _errors = new List<ErrorDetail>();

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public string SourceUrl
        {
            get => _sourceUrl;
            set { if (_sourceUrl != value) { _sourceUrl = value; OnPropertyChanged(); } }
        }

        public string SourceDomain
        {
            get => _sourceDomain;
            set { if (_sourceDomain != value) { _sourceDomain = value; OnPropertyChanged(); } }
        }

        public int TotalChapters
        {
            get => _totalChapters;
            set { if (_totalChapters != value) { _totalChapters = value; OnPropertyChanged(); } }
        }

        public int CompletedChapters
        {
            get => _completedChapters;
            set
            {
                if (_completedChapters != value)
                {
                    _completedChapters = value;
                    OnPropertyChanged();
                    if (_totalChapters > 0)
                    {
                        ProgressPercent = ((double)_completedChapters / _totalChapters) * 100;
                    }
                }
            }
        }

        public string Status
        {
            get => _status;
            set { if (_status != value) { _status = value; OnPropertyChanged(); } }
        }

        public string CurrentProcess
        {
            get => _currentProcess;
            set { if (_currentProcess != value) { _currentProcess = value; OnPropertyChanged(); } }
        }

        public int ErrorCount
        {
            get => _errorCount;
            set { if (_errorCount != value) { _errorCount = value; OnPropertyChanged(); } }
        }

        public string DownloadPath
        {
            get => _downloadPath;
            set { if (_downloadPath != value) { _downloadPath = value; OnPropertyChanged(); } }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set { if (_progressPercent != value) { _progressPercent = value; OnPropertyChanged(); } }
        }

        public List<ErrorDetail> Errors
        {
            get => _errors;
            set { if (_errors != value) { _errors = value; OnPropertyChanged(); } }
        }

        public void AddError(string chapterName, int pageNumber, string errorMessage, string imageUrl = null)
        {
            Errors.Add(new ErrorDetail
            {
                ChapterName = chapterName,
                PageNumber = pageNumber,
                ErrorMessage = errorMessage,
                ImageUrl = imageUrl
            });
            ErrorCount = Errors.Count;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
