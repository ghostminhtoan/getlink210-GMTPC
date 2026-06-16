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

    public class CheckErrorItem : INotifyPropertyChanged
    {
        private DateTime _lastSeen;
        private string _source;
        private string _bookName;
        private string _chapterName;
        private int _pageNumber;
        private string _errorMessage;
        private string _imageUrl;
        private int _occurrenceCount = 1;

        public DateTime LastSeen
        {
            get => _lastSeen;
            set
            {
                if (_lastSeen != value)
                {
                    _lastSeen = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Source
        {
            get => _source;
            set { if (_source != value) { _source = value; OnPropertyChanged(); } }
        }

        public string BookName
        {
            get => _bookName;
            set
            {
                if (_bookName != value)
                {
                    _bookName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayBook));
                }
            }
        }

        public string ChapterName
        {
            get => _chapterName;
            set
            {
                if (_chapterName != value)
                {
                    _chapterName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayBook));
                }
            }
        }

        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (_pageNumber != value)
                {
                    _pageNumber = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PageDisplay));
                    OnPropertyChanged(nameof(DisplayBook));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); } }
        }

        public string ImageUrl
        {
            get => _imageUrl;
            set { if (_imageUrl != value) { _imageUrl = value; OnPropertyChanged(); } }
        }

        public int OccurrenceCount
        {
            get => _occurrenceCount;
            set { if (_occurrenceCount != value) { _occurrenceCount = value; OnPropertyChanged(); } }
        }

        public string PageDisplay => PageNumber > 0 ? PageNumber.ToString() : "-";

        public string DisplayBook
        {
            get
            {
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(BookName) && BookName != "-")
                {
                    parts.Add(BookName.Trim());
                }

                if (!string.IsNullOrWhiteSpace(ChapterName) && ChapterName != "-")
                {
                    parts.Add(ChapterName.Trim());
                }

                if (PageNumber > 0)
                {
                    parts.Add($"trang {PageNumber}");
                }

                return parts.Count == 0 ? "-" : string.Join(" - ", parts);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        private string _chapterSelectionText;
        private double _progressPercent;
        private int _connectionCount = 1;
        private int _multiDownloadCount = 2;
        private List<ErrorDetail> _errors = new List<ErrorDetail>();
        private bool _isPaused;
        private bool _isStopped;
        private string _downloadingChapter;
        private string _downloadingPageProgress;
        private string _downloadingPageLink;
        private int _nhentaiTotalPagesHint;

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

        public int NhentaiTotalPagesHint
        {
            get => _nhentaiTotalPagesHint;
            set
            {
                if (_nhentaiTotalPagesHint != value)
                {
                    _nhentaiTotalPagesHint = value;
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
                    OnPropertyChanged(nameof(DisplayStatusText));
                    OnPropertyChanged(nameof(ShouldShowStatus));
                    OnPropertyChanged(nameof(DisplayDownloadingChapter));
                    OnPropertyChanged(nameof(DisplayDownloadingPageProgress));
                    OnPropertyChanged(nameof(ShouldShowProcess));
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
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusSortOrder));
                    OnPropertyChanged(nameof(DisplayStatusText));
                    OnPropertyChanged(nameof(ShouldShowStatus));
                    OnPropertyChanged(nameof(DisplayDownloadingChapter));
                    OnPropertyChanged(nameof(DisplayDownloadingPageProgress));
                    OnPropertyChanged(nameof(ShouldShowProcess));
                }
            }
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
                        _currentProcess = string.IsNullOrWhiteSpace(DownloadingPageProgress)
                            ? DownloadingChapter
                            : $"{DownloadingChapter} | {DownloadingPageProgress}";
                    }
                    else if (value.StartsWith("Trang ") || value.Contains("/"))
                    {
                        if (value.EndsWith(" pages"))
                        {
                            DownloadingPageProgress = $"Trang {value.Replace(" pages", "").Trim()}";
                        }
                        else
                        {
                            DownloadingPageProgress = value;
                        }
                        if (string.IsNullOrWhiteSpace(DownloadingChapter))
                        {
                            DownloadingChapter = "General";
                        }
                        _currentProcess = string.IsNullOrWhiteSpace(DownloadingChapter)
                            ? DownloadingPageProgress
                            : $"{DownloadingChapter} | {DownloadingPageProgress}";
                    }
                    else if (value.StartsWith("Retrying", StringComparison.OrdinalIgnoreCase) ||
                             value.StartsWith("Retry:", StringComparison.OrdinalIgnoreCase))
                    {
                        DownloadingChapter = "Retrying errors";
                        if (value.StartsWith("Retry:", StringComparison.OrdinalIgnoreCase))
                        {
                            DownloadingPageProgress = value.Replace("Retry:", "").Trim();
                        }
                        else if (value.StartsWith("Retrying errors", StringComparison.OrdinalIgnoreCase))
                        {
                            DownloadingPageProgress = value.Substring("Retrying errors".Length).Trim();
                        }
                        else
                        {
                            DownloadingPageProgress = value.Substring("Retrying".Length).Trim();
                        }
                        _currentProcess = $"{DownloadingChapter} | {DownloadingPageProgress}";
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(DownloadingChapter))
                        {
                            DownloadingChapter = value;
                        }
                        DownloadingPageProgress = value;
                    }

                    OnPropertyChanged(nameof(DisplayDownloadingChapter));
                    OnPropertyChanged(nameof(DisplayDownloadingPageProgress));
                    OnPropertyChanged(nameof(ProcessSortText));
                    OnPropertyChanged(nameof(ShouldShowProcess));
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

        public string ChapterSelectionText
        {
            get => _chapterSelectionText;
            set
            {
                if (_chapterSelectionText != value)
                {
                    _chapterSelectionText = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set { if (_progressPercent != value) { _progressPercent = value; OnPropertyChanged(); } }
        }

        public int ConnectionCount
        {
            get => _connectionCount;
            set
            {
                if (_connectionCount != value)
                {
                    _connectionCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ConnectionDisplayText));
                }
            }
        }

        public int MultiDownloadCount
        {
            get => _multiDownloadCount;
            set { if (_multiDownloadCount != value) { _multiDownloadCount = value; OnPropertyChanged(); } }
        }

        public List<ErrorDetail> Errors
        {
            get => _errors;
            set { if (_errors != value) { _errors = value; OnPropertyChanged(); } }
        }

        public string ConnectionDisplayText => $"{ConnectionCount} conn";

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
            set
            {
                if (_downloadingChapter != value)
                {
                    _downloadingChapter = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayDownloadingChapter));
                    OnPropertyChanged(nameof(ProcessSortText));
                    OnPropertyChanged(nameof(ShouldShowProcess));
                }
            }
        }

        public string DownloadingPageProgress
        {
            get => _downloadingPageProgress;
            set
            {
                if (_downloadingPageProgress != value)
                {
                    _downloadingPageProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayDownloadingPageProgress));
                    OnPropertyChanged(nameof(ProcessSortText));
                    OnPropertyChanged(nameof(ShouldShowProcess));
                }
            }
        }

        private bool ShouldHideQueuedDisplay =>
            !_isChecked &&
            string.Equals(_status, "Queued", StringComparison.OrdinalIgnoreCase);

        private static bool IsVietnameseUiEnabled()
        {
            try
            {
                return System.Windows.Application.Current?.Properties["IsVietnameseUi"] is bool isVietnamese && isVietnamese;
            }
            catch
            {
                return false;
            }
        }

        private static string GetLocalizedLabel(string english, string vietnamese)
        {
            return IsVietnameseUiEnabled() ? vietnamese : english;
        }

        private static string NormalizePageProgressText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            string lower = trimmed.ToLowerInvariant();

            if (lower.StartsWith("trang "))
            {
                string pageValue = trimmed.Substring("trang ".Length).Trim();
                return $"{GetLocalizedLabel("Page", "Trang")} {pageValue}";
            }

            if (lower.StartsWith("page "))
            {
                string pageValue = trimmed.Substring("page ".Length).Trim();
                return $"{GetLocalizedLabel("Page", "Trang")} {pageValue}";
            }

            if (trimmed.Contains("/"))
            {
                return $"{GetLocalizedLabel("Page", "Trang")} {trimmed}";
            }

            return trimmed;
        }

        public bool ShouldShowStatus =>
            !string.IsNullOrWhiteSpace(DisplayStatusText);

        public string DisplayStatusText
        {
            get
            {
                if (ShouldHideQueuedDisplay)
                {
                    return string.Empty;
                }

                switch (_status)
                {
                    case "Queued":
                        return IsVietnameseUiEnabled() ? "⏳ Chờ tải" : "⏳ Waiting";
                    case "Downloading":
                        return IsVietnameseUiEnabled() ? "⬇️ Đang tải" : "⬇️ Downloading";
                    case "Completed":
                        return IsVietnameseUiEnabled() ? "✅ Hoàn tất" : "✅ Done";
                    case "Error":
                        return IsVietnameseUiEnabled() ? "❌ Lỗi" : "❌ Error";
                    case "Paused":
                        return IsVietnameseUiEnabled() ? "⏸️ Tạm dừng" : "⏸️ Paused";
                    case "Stopping":
                        return IsVietnameseUiEnabled() ? "🛑 Đang dừng" : "🛑 Stopping";
                    case "Cancelled":
                        return IsVietnameseUiEnabled() ? "⛔ Đã dừng" : "⛔ Stopped";
                    default:
                        return string.IsNullOrWhiteSpace(_status) ? string.Empty : _status;
                }
            }
        }

        public string DownloadingPageLink
        {
            get => _downloadingPageLink;
            set
            {
                if (_downloadingPageLink != value)
                {
                    _downloadingPageLink = value;
                    OnPropertyChanged();
                }
            }
        }

        public int StatusSortOrder
        {
            get
            {
                switch ((_status ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "downloading":
                        return 1;
                    case "queued":
                        return 2;
                    case "paused":
                        return 3;
                    case "error":
                        return 4;
                    case "completed":
                        return 5;
                    case "cancelled":
                        return 6;
                    case "stopping":
                        return 7;
                    default:
                        return 99;
                }
            }
        }

        public string DisplayDownloadingChapter =>
            ShouldHideQueuedDisplay ? string.Empty : _downloadingChapter;

        public string DisplayDownloadingPageProgress =>
            ShouldHideQueuedDisplay ? string.Empty : NormalizePageProgressText(_downloadingPageProgress);

        public string ProcessSortText =>
            $"{DisplayDownloadingChapter ?? string.Empty}|{DisplayDownloadingPageProgress ?? string.Empty}";

        public bool ShouldShowProcess =>
            !string.IsNullOrWhiteSpace(DisplayDownloadingChapter) ||
            !string.IsNullOrWhiteSpace(DisplayDownloadingPageProgress);

        public void RefreshDisplayText()
        {
            OnPropertyChanged(nameof(DisplayStatusText));
            OnPropertyChanged(nameof(ShouldShowStatus));
            OnPropertyChanged(nameof(DisplayDownloadingChapter));
            OnPropertyChanged(nameof(DisplayDownloadingPageProgress));
            OnPropertyChanged(nameof(ShouldShowProcess));
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

        public bool HasAnyErrors()
        {
            return GetUniqueErrorCount() > 0;
        }

        public bool IsSuccessfullyCompleted()
        {
            return string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                   !HasAnyErrors() &&
                   !string.Equals(CurrentProcess, "Done with errors", StringComparison.OrdinalIgnoreCase);
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
