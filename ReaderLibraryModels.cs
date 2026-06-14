using System;
using System.Collections.Generic;

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

        public string DisplayLabel => $"Page {Index + 1:000} - {Name}";

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    internal sealed class ReaderChapterItem
    {
        public string Name { get; set; }

        public string FolderPath { get; set; }

        public List<ReaderPageItem> Pages { get; set; } = new List<ReaderPageItem>();

        public bool IsCompleted { get; set; }

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

        public List<ReaderChapterItem> Chapters { get; set; } = new List<ReaderChapterItem>();

        public bool IsCompleted { get; set; }

        public string DisplayLabel
        {
            get
            {
                string prefix = string.IsNullOrWhiteSpace(SourceGroup) ? string.Empty : SourceGroup + " - ";
                string suffix = IsCompleted ? " - completed" : string.Empty;
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

        public List<ReaderMangaItem> Books { get; set; } = new List<ReaderMangaItem>();

        public string DisplayLabel => $"{Name} ({Books.Count} book{(Books.Count == 1 ? string.Empty : "s")})";

        public override string ToString()
        {
            return DisplayLabel;
        }
    }
}
