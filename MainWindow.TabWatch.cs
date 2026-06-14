using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private Border CreateReaderWatchPanel(string title, UIElement content)
        {
            if (content is FrameworkElement element)
            {
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment = VerticalAlignment.Stretch;
            }

            var panel = new Border
            {
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                    },
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        content
                    }
                }
            };

            if (content != null)
            {
                Grid.SetRow(content, 1);
            }

            return panel;
        }

        private List<ReaderDomainItem> BuildReaderDomainItems(IReadOnlyList<ReaderMangaItem> books)
        {
            var domains = new List<ReaderDomainItem>();
            if (books == null || books.Count == 0)
            {
                return domains;
            }

            IEnumerable<IGrouping<string, ReaderMangaItem>> groupedBooks = books
                .GroupBy(item => string.IsNullOrWhiteSpace(item.SourceGroup) ? "root" : item.SourceGroup, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, ReaderMangaItem> group in groupedBooks)
            {
                domains.Add(new ReaderDomainItem
                {
                    Name = group.Key,
                    Books = group
                        .OrderBy(item => item.Name, _readerSortComparer)
                        .ToList()
                });
            }

            return domains;
        }

        private void UpdateReaderDomainListItems(IList<ReaderDomainItem> domains)
        {
            if (_readerDomainList == null)
            {
                return;
            }

            _readerDomainList.ItemsSource = domains;
        }

        private void SyncReaderDomainListSelection(ReaderDomainItem domain)
        {
            if (_readerDomainList == null)
            {
                return;
            }

            _readerSelectionGuard = true;
            _readerDomainList.SelectedItem = domain;
            _readerSelectionGuard = false;
        }

        private void ClearReaderDomainList()
        {
            if (_readerDomainList == null)
            {
                return;
            }

            _readerDomainList.ItemsSource = null;
            _readerDomainList.SelectedItem = null;
        }

        private void UpdateReaderBookListItems(IList<ReaderMangaItem> books)
        {
            if (_readerMangaList == null)
            {
                return;
            }

            _readerMangaList.ItemsSource = books;
        }

        private void SyncReaderBookListSelection(ReaderMangaItem book)
        {
            if (_readerMangaList == null)
            {
                return;
            }

            _readerSelectionGuard = true;
            _readerMangaList.SelectedItem = book;
            _readerSelectionGuard = false;
        }

        private void ClearReaderBookList()
        {
            if (_readerMangaList == null)
            {
                return;
            }

            _readerMangaList.ItemsSource = null;
            _readerMangaList.SelectedItem = null;
        }

        private void UpdateReaderChapterListItems(IList<ReaderChapterItem> chapters)
        {
            if (_readerChapterList == null)
            {
                return;
            }

            _readerChapterList.ItemsSource = chapters;
        }

        private void SyncReaderChapterListSelection(ReaderChapterItem chapter)
        {
            if (_readerChapterList == null)
            {
                return;
            }

            _readerSelectionGuard = true;
            _readerChapterList.SelectedItem = chapter;
            _readerSelectionGuard = false;
        }

        private void ClearReaderChapterList()
        {
            if (_readerChapterList == null)
            {
                return;
            }

            _readerChapterList.ItemsSource = null;
            _readerChapterList.SelectedItem = null;
        }

        private void UpdateReaderFileListItems(IList<ReaderPageItem> pages)
        {
            if (_readerFileList == null)
            {
                return;
            }

            _readerFileList.ItemsSource = pages;
        }

        private void SyncReaderFileListSelection(ReaderPageItem page)
        {
            if (_readerFileList == null)
            {
                return;
            }

            _readerSelectionGuard = true;
            _readerFileList.SelectedItem = page;
            _readerSelectionGuard = false;
        }

        private void ClearReaderFileList()
        {
            if (_readerFileList == null)
            {
                return;
            }

            _readerFileList.ItemsSource = null;
            _readerFileList.SelectedItem = null;
        }
    }
}
