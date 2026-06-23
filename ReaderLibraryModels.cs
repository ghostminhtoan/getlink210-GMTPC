using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace get_link_manga
{
    internal sealed class UiZoomPreset
    {
        public UiZoomPreset(int percent)
        {
            Percent = percent;
        }

        public int Percent { get; }

        public override string ToString()
        {
            return Percent + "%";
        }
    }

    internal sealed class ReaderPageItem
    {
        public int Index { get; set; }

        public string Name { get; set; }

        public string FilePath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public string DisplayLabel => $"Page {Index + 1:000} - {Name}";

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderMarkdownItem
    {
        public int Index { get; set; }

        public string Name { get; set; }

        public string FilePath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public string DisplayLabel => $"MD {Index + 1:000} - {Name}";

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderChapterItem
    {
        public string Name { get; set; }

        public string FolderPath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public List<ReaderPageItem> Pages { get; set; } = new List<ReaderPageItem>();

        public bool IsCompleted { get; set; }

        public double? ParsedChapterNumber { get; set; }

        public bool IsDecimalChapter { get; set; }

        public bool HasMissingIntegerGap { get; set; }

        public Brush DisplayForeground { get; set; }

        public string DisplayLabel
        {
            get
            {
                string suffix = IsCompleted ? " - completed" : string.Empty;
                return $"{Name} ({Pages.Count} page{(Pages.Count == 1 ? string.Empty : "s")}){suffix}";
            }
        }

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderMangaItem
    {
        public string Name { get; set; }

        public string SourceGroup { get; set; }

        public string FolderPath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public List<ReaderChapterItem> Chapters { get; set; } = new List<ReaderChapterItem>();

        public bool IsCompleted { get; set; }

        public string DownloadStateText { get; set; }

        public string DisplayLabel
        {
            get
            {
                string prefix = string.IsNullOrWhiteSpace(SourceGroup) ? string.Empty : SourceGroup + " - ";
                string suffix = IsCompleted ? " - completed" : string.Empty;
                if (!string.IsNullOrWhiteSpace(DownloadStateText))
                {
                    suffix = " - " + DownloadStateText.Trim().ToLowerInvariant();
                }
                return $"{prefix}{Name} ({Chapters.Count} chap{(Chapters.Count == 1 ? string.Empty : "ters")}){suffix}";
            }
        }

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderDomainItem
    {
        public string Name { get; set; }

        public string FolderPath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public List<ReaderMangaItem> Books { get; set; } = new List<ReaderMangaItem>();

        public string DownloadStateText { get; set; }

        public string DisplayLabel
        {
            get
            {
                string suffix = string.IsNullOrWhiteSpace(DownloadStateText)
                    ? string.Empty
                    : " - " + DownloadStateText.Trim().ToLowerInvariant();
                return $"{Name} ({Books.Count} book{(Books.Count == 1 ? string.Empty : "s")}){suffix}";
            }
        }

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderNovelChapterItem
    {
        public string Name { get; set; }

        public string FolderPath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public List<ReaderMarkdownItem> Files { get; set; } = new List<ReaderMarkdownItem>();

        public string DisplayLabel => $"{Name} ({Files.Count} md)";

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderNovelBookItem
    {
        public string Name { get; set; }

        public string SourceGroup { get; set; }

        public string FolderPath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public List<ReaderNovelChapterItem> Chapters { get; set; } = new List<ReaderNovelChapterItem>();

        public string DisplayLabel
        {
            get
            {
                string prefix = string.IsNullOrWhiteSpace(SourceGroup) ? string.Empty : SourceGroup + " - ";
                return $"{prefix}{Name} ({Chapters.Count} chapter{(Chapters.Count == 1 ? string.Empty : "s")})";
            }
        }

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderNovelDomainItem
    {
        public string Name { get; set; }

        public string FolderPath { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        public List<ReaderNovelBookItem> Books { get; set; } = new List<ReaderNovelBookItem>();

        public string DisplayLabel => $"{Name} ({Books.Count} book{(Books.Count == 1 ? string.Empty : "s")})";

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderChapterIssueItem
    {
        public string BookName { get; set; }

        public string ChapterLabel { get; set; }

        public string MissingChapterLabel { get; set; }

        public string DecimalChapterLabel { get; set; }

        public ReaderChapterItem ChapterTarget { get; set; }

        public ReaderChapterItem MissingTarget { get; set; }

        public ReaderChapterItem DecimalTarget { get; set; }

        public bool HasMissingChapter => !string.IsNullOrWhiteSpace(MissingChapterLabel);

        public bool HasDecimalChapter => !string.IsNullOrWhiteSpace(DecimalChapterLabel);
    }
}
