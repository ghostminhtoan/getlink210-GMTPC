using System;
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

    public class GalleryItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private string _link;
        private string _name;
        private int _originalIndex;
        private int _linkCount;
        private bool _isDuplicate;
        private bool _hasNoChapters;

        // Downloading status fields
        private string _sourceDomain;
        private int _totalChapters;
        private int _completedChapters;
        private string _status;
        private string _currentProcess;
        private int _errorCount;
        private string _downloadPath;
        private double _progressPercent;
        private List<ErrorDetail> _errors = new List<ErrorDetail>();
        private bool _isPaused;
        private bool _isStopped;

        public bool HasNoChapters
        {
            get => _hasNoChapters;
            set
            {
                if (_hasNoChapters != value)
                {
                    _hasNoChapters = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDuplicate
        {
            get => _isDuplicate;
            set
            {
                if (_isDuplicate != value)
                {
                    _isDuplicate = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Link
        {
            get => _link;
            set
            {
                if (_link != value)
                {
                    _link = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int OriginalIndex
        {
            get => _originalIndex;
            set
            {
                if (_originalIndex != value)
                {
                    _originalIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public int LinkCount
        {
            get => _linkCount;
            set
            {
                if (_linkCount != value)
                {
                    _linkCount = value;
                    OnPropertyChanged();
                }
            }
        }

        // New properties for downloading
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

        public bool IsPaused
        {
            get => _isPaused;
            set { if (_isPaused != value) { _isPaused = value; OnPropertyChanged(); } }
        }

        public bool IsStopped
        {
            get => _isStopped;
            set { if (_isStopped != value) { _isStopped = value; OnPropertyChanged(); } }
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
