using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        private string _linkCount = "";
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
        private string _downloadingChapter;
        private string _downloadingPageProgress;

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

        public string LinkCount
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
            set
            {
                if (_currentProcess != value)
                {
                    _currentProcess = value;
                    OnPropertyChanged();

                    if (string.IsNullOrEmpty(value))
                    {
                        DownloadingChapter = "";
                        DownloadingPageProgress = "";
                    }
                    else if (value.Contains(" (trang "))
                    {
                        int idx = value.IndexOf(" (trang ");
                        DownloadingChapter = value.Substring(0, idx).Trim();
                        string p = value.Substring(idx + " (trang ".Length).Replace(")", "").Trim();
                        DownloadingPageProgress = $"Trang {p}";
                    }
                    else if (value.StartsWith("Trang ") || value.Contains("/"))
                    {
                        DownloadingChapter = "General";
                        if (value.EndsWith(" pages"))
                        {
                            DownloadingPageProgress = $"Trang {value.Replace(" pages", "").Trim()}";
                        }
                        else
                        {
                            DownloadingPageProgress = value;
                        }
                    }
                    else if (value.StartsWith("Retry:"))
                    {
                        DownloadingChapter = "Retrying errors";
                        DownloadingPageProgress = value.Replace("Retry:", "").Trim();
                    }
                    else
                    {
                        DownloadingChapter = value;
                        DownloadingPageProgress = "";
                    }
                }
            }
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

        public string DownloadingChapter
        {
            get => _downloadingChapter;
            set { if (_downloadingChapter != value) { _downloadingChapter = value; OnPropertyChanged(); } }
        }

        public string DownloadingPageProgress
        {
            get => _downloadingPageProgress;
            set { if (_downloadingPageProgress != value) { _downloadingPageProgress = value; OnPropertyChanged(); } }
        }

        public List<ErrorDetail> GetUniqueErrors()
        {
            var uniqueErrors = new List<ErrorDetail>();
            if (Errors == null || Errors.Count == 0)
            {
                return uniqueErrors;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var error in Errors)
            {
                if (error == null)
                {
                    continue;
                }

                string key = BuildErrorKey(error.ChapterName, error.PageNumber);
                if (seen.Add(key))
                {
                    uniqueErrors.Add(error);
                }
            }

            return uniqueErrors;
        }

        public int GetUniqueErrorCount()
        {
            return GetUniqueErrors().Count;
        }

        public void AddError(string chapterName, int pageNumber, string errorMessage, string imageUrl = null)
        {
            if (Errors == null)
            {
                Errors = new List<ErrorDetail>();
            }

            var existing = Errors.FirstOrDefault(e =>
                e != null &&
                string.Equals((e.ChapterName ?? string.Empty).Trim(), (chapterName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                e.PageNumber == pageNumber);

            if (existing != null)
            {
                existing.ErrorMessage = errorMessage;
                existing.ImageUrl = imageUrl;
            }
            else
            {
                Errors.Add(new ErrorDetail
                {
                    ChapterName = chapterName,
                    PageNumber = pageNumber,
                    ErrorMessage = errorMessage,
                    ImageUrl = imageUrl
                });
            }

            ErrorCount = GetUniqueErrorCount();
        }

        private static string BuildErrorKey(string chapterName, int pageNumber)
        {
            return $"{(chapterName ?? string.Empty).Trim().ToUpperInvariant()}::{pageNumber}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
