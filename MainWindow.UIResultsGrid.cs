using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private ICollectionView ResultsView => CollectionViewSource.GetDefaultView(_scrapedItems);

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = ResultsView;
            if (view != null)
            {
                string filterText = txtFilter.Text.Trim();
                if (string.IsNullOrEmpty(filterText))
                {
                    view.Filter = null;
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item is GalleryItem galleryItem)
                        {
                            return galleryItem.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   galleryItem.Link.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        return false;
                    };
                }
            }
        }

        private void BtnSortByName_Click(object sender, RoutedEventArgs e)
        {
            var view = ResultsView;
            if (view != null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                Log("Results sorted alphabetically by name.");
            }
        }

        private void BtnRestoreOrder_Click(object sender, RoutedEventArgs e)
        {
            var view = ResultsView;
            if (view != null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Ascending));
                Log("Original order restored.");
            }
        }

        private void BtnNoLinkViHentai_Click(object sender, RoutedEventArgs e)
        {
            var view = ResultsView;
            if (view != null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("HasNoChapters", ListSortDirection.Descending));
                view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Ascending));
                Log("Results sorted to show vi-hentai galleries with no chapters first.");
            }
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                bool isChecked = chk.IsChecked ?? false;
                foreach (var item in _scrapedItems)
                {
                    item.IsChecked = isChecked;
                }
                Log($"{(isChecked ? "Checked" : "Unchecked")} all items via header checkbox.");
            }
        }

        private void DgResults_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (dgResults.Items.Count == 0) return;

            if (e.Key == Key.Home)
            {
                dgResults.SelectedIndex = 0;
                dgResults.ScrollIntoView(dgResults.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.End)
            {
                dgResults.SelectedIndex = dgResults.Items.Count - 1;
                dgResults.ScrollIntoView(dgResults.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                DeleteSelectedItems();
                e.Handled = true;
            }
        }

        private void DeleteSelectedItems()
        {
            if (dgResults.SelectedItems.Count == 0) return;

            var itemsToRemove = dgResults.SelectedItems.Cast<GalleryItem>().ToList();
            foreach (var item in itemsToRemove)
            {
                _scrapedItems.Remove(item);
            }
            
            lblLinkCount.Text = _scrapedItems.Count.ToString();
            Log($"Deleted {itemsToRemove.Count} selected item(s).");
            lblStatus.Text = $"Deleted {itemsToRemove.Count} item(s).";
            
            RecalculateDuplicates();
        }

        private void DgResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            while (element != null && !(element is DataGridRow))
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is DataGridRow row && row.Item is GalleryItem item)
            {
                if (!string.IsNullOrEmpty(item.Link))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = item.Link,
                            UseShellExecute = true
                        });
                        Log($"Opened link: {item.Link}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to open link: {ex.Message}");
                    }
                }
            }
        }

        private void MenuCheckSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgResults.SelectedItems.Cast<GalleryItem>())
            {
                item.IsChecked = true;
            }
            Log("Checked selected items.");
        }

        private void MenuUncheckSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgResults.SelectedItems.Cast<GalleryItem>())
            {
                item.IsChecked = false;
            }
            Log("Unchecked selected items.");
        }

        private void MenuInvertChecked_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _scrapedItems)
            {
                item.IsChecked = !item.IsChecked;
            }
            Log("Inverted checked status for all items.");
        }

        private void MenuDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedItems();
        }

        private void MenuCopySelectedLinks_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItems.Count == 0) return;
            var items = dgResults.SelectedItems.Cast<GalleryItem>().ToList();
            string text = string.Join("\r\n", items.Select(item => item.Link));
            Clipboard.SetText(text);
            Log($"Copied {items.Count} selected link(s) to clipboard.");
        }

        private string _searchBuffer = "";
        private DateTime _lastKeyPressTime = DateTime.MinValue;

        private void DgResults_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            DateTime now = DateTime.Now;
            if ((now - _lastKeyPressTime).TotalMilliseconds > 1000)
            {
                _searchBuffer = "";
            }
            _lastKeyPressTime = now;
            _searchBuffer += e.Text;

            var items = dgResults.Items.Cast<GalleryItem>().ToList();
            var match = items.FirstOrDefault(item => item.Name.StartsWith(_searchBuffer, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                match = items.FirstOrDefault(item => item.Name.IndexOf(_searchBuffer, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (match != null)
            {
                dgResults.SelectedItem = match;
                dgResults.ScrollIntoView(match);
                
                var row = (DataGridRow)dgResults.ItemContainerGenerator.ContainerFromItem(match);
                if (row != null)
                {
                    row.Focus();
                }
            }

            e.Handled = true;
        }

        public static string GetSimilarityCore(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            string core = name.ToLower();

            // 1. Remove brackets [...] and their contents
            core = Regex.Replace(core, @"\[[^\]]*\]", "");

            // 2. Remove curly braces {...} and their contents
            core = Regex.Replace(core, @"\{[^\}]*\}", "");

            // 3. Remove parentheses (...) and their contents
            core = Regex.Replace(core, @"\([^\)]*\)", "");

            // 4. Remove common variation/part keywords in proper order (longest first)
            string[] keywords = new string[]
            {
                @"extra\s+version", @"copy\s+of",
                @"part\s+\d+", @"part\d+", @"pt\s+\d+", @"pt\d+", @"vol\s+\d+", @"vol\d+",
                @"ch\s+\d+", @"ch\d+", @"chap\s+\d+", @"chap\d+", @"chapter\s+\d+", @"chapter\d+",
                @"minidoujin", @"doujinshi", @"decensored", @"uncensored",
                @"colorized", @"censored", @"colored",
                @"extra", @"extras", @"version",
                @"rewrite", @"copy", @"doujin", @"dj",
                @"\bch\b", @"\bchap\b", @"\bpart\b", @"\bpt\b", @"\bvol\b"
            };

            foreach (var kw in keywords)
            {
                core = Regex.Replace(core, kw, "");
            }

            // 5. Remove numbers at the end of words or standalone
            core = Regex.Replace(core, @"\b\d+\b", "");

            // 6. Remove non-alphanumeric characters
            core = Regex.Replace(core, @"[^a-z0-9]", "");

            return core.Trim();
        }

        public void RecalculateDuplicates()
        {
            var groups = _scrapedItems
                .GroupBy(item => GetSimilarityCore(item.Name))
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToList();

            foreach (var item in _scrapedItems)
            {
                item.IsDuplicate = false;
            }

            foreach (var group in groups)
            {
                if (group.Count() > 1)
                {
                    foreach (var item in group)
                    {
                        item.IsDuplicate = true;
                    }
                }
            }
        }

        private void BtnDuplicateName_Click(object sender, RoutedEventArgs e)
        {
            RecalculateDuplicates();
            
            if (_duplicateWindowInstance != null)
            {
                if (_duplicateWindowInstance.WindowState == WindowState.Minimized)
                {
                    _duplicateWindowInstance.WindowState = WindowState.Normal;
                }
                _duplicateWindowInstance.Activate();
            }
            else
            {
                _duplicateWindowInstance = new DuplicateWindow(this);
                _duplicateWindowInstance.Owner = this;
                _duplicateWindowInstance.Closed += (s, args) => { _duplicateWindowInstance = null; };
                _duplicateWindowInstance.Show();
            }
        }

        private void MenuDeleteChecked_Click(object sender, RoutedEventArgs e)
        {
            DeleteCheckedItems();
        }

        private void DeleteCheckedItems()
        {
            var itemsToRemove = _scrapedItems.Where(item => item.IsChecked).ToList();
            if (!itemsToRemove.Any()) return;

            foreach (var item in itemsToRemove)
            {
                _scrapedItems.Remove(item);
            }

            lblLinkCount.Text = _scrapedItems.Count.ToString();
            Log($"Deleted {itemsToRemove.Count} checked item(s).");
            lblStatus.Text = $"Deleted {itemsToRemove.Count} checked item(s).";

            RecalculateDuplicates();
        }

        private async void MenuDownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            var items = dgResults.SelectedItems.Cast<GalleryItem>().ToList();
            if (!items.Any())
            {
                MessageBox.Show("Vui lòng bôi đen chọn ít nhất 1 dòng để tải (Please select at least one highlighted line to download).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await StartDownloadProcessAsync(items);
        }

        private async void MenuDownloadChecked_Click(object sender, RoutedEventArgs e)
        {
            var items = dgResults.Items.Cast<GalleryItem>().Where(item => item.IsChecked).ToList();
            if (!items.Any())
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 truyện để tải (Please check at least one gallery to download).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await StartDownloadProcessAsync(items);
        }
    }
}
