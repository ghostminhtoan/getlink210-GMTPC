using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private const string GalleryStateBeginMarker = "<!-- COMIC_GMTPC_STATE_BEGIN -->";
        private const string GalleryStateEndMarker = "<!-- COMIC_GMTPC_STATE_END -->";

        [DataContract]
        private sealed class GalleryItemState
        {
            [DataMember(Order = 1)]
            public int OriginalIndex { get; set; }

            [DataMember(Order = 2)]
            public bool IsChecked { get; set; }

            [DataMember(Order = 3)]
            public bool IsDuplicate { get; set; }

            [DataMember(Order = 4)]
            public bool HasNoChapters { get; set; }

            [DataMember(Order = 5)]
            public string Name { get; set; }

            [DataMember(Order = 6)]
            public string Link { get; set; }

            [DataMember(Order = 7)]
            public string LinkCount { get; set; }

            [DataMember(Order = 8)]
            public string SourceDomain { get; set; }

            [DataMember(Order = 9)]
            public int TotalChapters { get; set; }

            [DataMember(Order = 10)]
            public int CompletedChapters { get; set; }

            [DataMember(Order = 11)]
            public string Status { get; set; }

            [DataMember(Order = 12)]
            public string CurrentProcess { get; set; }

            [DataMember(Order = 13)]
            public int ErrorCount { get; set; }

            [DataMember(Order = 14)]
            public string DownloadPath { get; set; }

            [DataMember(Order = 15)]
            public double ProgressPercent { get; set; }

            [DataMember(Order = 16)]
            public bool IsPaused { get; set; }

            [DataMember(Order = 17)]
            public bool IsStopped { get; set; }

            [DataMember(Order = 18)]
            public string DownloadingChapter { get; set; }

            [DataMember(Order = 19)]
            public string DownloadingPageProgress { get; set; }

            [DataMember(Order = 20)]
            public List<ErrorState> Errors { get; set; } = new List<ErrorState>();

            [DataMember(Order = 21)]
            public int ConnectionCount { get; set; }

            [DataMember(Order = 22)]
            public int MultiDownloadCount { get; set; }
        }

        [DataContract]
        private sealed class ErrorState
        {
            [DataMember(Order = 1)]
            public string ChapterName { get; set; }

            [DataMember(Order = 2)]
            public int PageNumber { get; set; }

            [DataMember(Order = 3)]
            public string ErrorMessage { get; set; }

            [DataMember(Order = 4)]
            public string ImageUrl { get; set; }
        }

        private List<GalleryItem> GetItemsToExport()
        {
            var checkedItems = _scrapedItems.Where(item => item.IsChecked).ToList();
            return checkedItems.Any() ? checkedItems : _scrapedItems.ToList();
        }

        private void BtnCopyLinksOnly_Click(object sender, RoutedEventArgs e)
        {
            var items = GetItemsToExport();
            if (!items.Any())
            {
                MessageBox.Show("No links to copy.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string text = string.Join("\r\n", items.Select(item => item.Link));
            Clipboard.SetText(text);

            bool showingSubset = items.Count < _scrapedItems.Count;
            string scopeText = showingSubset ? $"{items.Count} checked" : "All";
            Log($"{scopeText} links copied to clipboard (URLs only).");
            lblStatus.Text = $"Links copied ({scopeText} URLs only).";
        }

        private void BtnCopyNamesAndLinks_Click(object sender, RoutedEventArgs e)
        {
            var items = GetItemsToExport();
            if (!items.Any())
            {
                MessageBox.Show("No content to copy.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string text = string.Join("\r\n", items.Select(item => $"{item.Name}\t{item.Link}"));
            Clipboard.SetText(text);

            bool showingSubset = items.Count < _scrapedItems.Count;
            string scopeText = showingSubset ? $"{items.Count} checked" : "All";
            Log($"{scopeText} titles + links copied to clipboard in tabular format (ready for Excel/Word).");
            lblStatus.Text = $"Titles + links copied ({scopeText} table format).";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var items = _scrapedItems.ToList();
            if (!items.Any())
            {
                MessageBox.Show("No content to save.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = "save gallery.md"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var states = items.Select((item, index) => CreateGalleryItemState(item, index)).ToList();

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# Scraped HentaiForce Galleries");
                    sb.AppendLine();
                    sb.AppendLine("## Summary");
                    sb.AppendLine("| No. | Checked | Name | Link | Chapter | Page | Status | Process | Error Count |");
                    sb.AppendLine("| :--- | :---: | :--- | :--- | :--- | :--- | :--- | :--- | :---: |");

                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        string checkedStr = item.IsChecked ? "[x]" : "[ ]";
                        string status = EscapeMarkdownCell(item.Status);
                        string process = EscapeMarkdownCell(item.CurrentProcess);
                        string safeName = EscapeMarkdownCell(item.Name);
                        string safeLink = EscapeMarkdownCell(item.Link);
                        string chapter = EscapeMarkdownCell(item.DownloadingChapter);
                        string page = EscapeMarkdownCell(item.DownloadingPageProgress);
                        sb.AppendLine($"| {i + 1} | {checkedStr} | {safeName} | {safeLink} | {chapter} | {page} | {status} | {process} | {item.GetUniqueErrorCount()} |");
                    }

                    sb.AppendLine();
                    sb.AppendLine("## Full State");
                    sb.AppendLine(GalleryStateBeginMarker);
                    sb.AppendLine("```json");
                    sb.AppendLine(SerializeGalleryStates(states));
                    sb.AppendLine("```");
                    sb.AppendLine(GalleryStateEndMarker);

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), new UTF8Encoding(true));

                    Log($"All content successfully saved to Markdown file: {saveFileDialog.FileName}");
                    lblStatus.Text = "Saved to MD file.";
                }
                catch (Exception ex)
                {
                    Log($"Failed to save file: {ex.Message}");
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string content = File.ReadAllText(openFileDialog.FileName, Encoding.UTF8);
                    List<GalleryItem> loadedItems = LoadGalleryItemsFromMarkdown(content);

                    if (loadedItems.Any())
                    {
                        _scrapedItems.Clear();
                        if (chkSelectAll != null)
                        {
                            chkSelectAll.IsChecked = false;
                        }

                        for (int i = 0; i < loadedItems.Count; i++)
                        {
                            var item = loadedItems[i];
                            item.OriginalIndex = i;
                            _scrapedItems.Add(item);
                        }

                        RecalculateDuplicates();
                        lblLinkCount.Text = _scrapedItems.Count.ToString();
                        ApplySavedDownloadSettings(loadedItems.FirstOrDefault());
                        Log($"Successfully loaded {_scrapedItems.Count} items from: {openFileDialog.FileName}");
                        lblStatus.Text = "Markdown file loaded successfully.";
                    }
                    else
                    {
                        MessageBox.Show("No valid entries found in the markdown file.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to load file: {ex.Message}");
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _scrapedItems.Clear();
            if (chkSelectAll != null)
            {
                chkSelectAll.IsChecked = false;
            }
            lblLinkCount.Text = "0";
            Log("Extracted results cleared.");
            lblStatus.Text = "Results cleared.";
            RecalculateDuplicates();
        }

        public void UpdateLinkCount()
        {
            Dispatcher.Invoke(() =>
            {
                lblLinkCount.Text = _scrapedItems.Count.ToString();
            });
        }

        private List<GalleryItem> LoadGalleryItemsFromMarkdown(string content)
        {
            var stateItems = TryLoadGalleryStateItems(content);
            if (stateItems != null && stateItems.Any())
            {
                return stateItems
                    .OrderBy(item => item.OriginalIndex)
                    .Select(CreateGalleryItemFromState)
                    .Where(item => item != null)
                    .ToList();
            }

            return LoadLegacyGalleryItems(content);
        }

        private List<GalleryItem> LoadLegacyGalleryItems(string content)
        {
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            List<GalleryItem> loadedItems = new List<GalleryItem>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
                {
                    var parts = trimmed.Split('|');
                    if (parts.Length >= 4)
                    {
                        string noStr = parts[1].Trim();
                        string name = parts[2].Trim();
                        string link = parts[3].Trim();

                        if (noStr.Equals("No.", StringComparison.OrdinalIgnoreCase) ||
                            noStr.Contains("---") ||
                            noStr.Contains(":") ||
                            string.IsNullOrEmpty(noStr))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(link) && link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            name = name.Replace("\\|", "|");
                            bool isChecked = false;
                            if (parts.Length >= 5)
                            {
                                string selStr = parts[4].Trim();
                                isChecked = selStr.Contains("[x]") || selStr.Equals("Yes", StringComparison.OrdinalIgnoreCase) || selStr.Equals("1");
                            }

                            if (!loadedItems.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                loadedItems.Add(new GalleryItem
                                {
                                    Name = name,
                                    Link = link,
                                    OriginalIndex = loadedItems.Count,
                                    IsChecked = isChecked
                                });
                            }
                        }
                    }
                }
            }

            return loadedItems;
        }

        private List<GalleryItemState> TryLoadGalleryStateItems(string content)
        {
            int beginMarkerIndex = content.IndexOf(GalleryStateBeginMarker, StringComparison.Ordinal);
            if (beginMarkerIndex < 0)
            {
                return null;
            }

            int jsonFenceStart = content.IndexOf("```json", beginMarkerIndex, StringComparison.OrdinalIgnoreCase);
            if (jsonFenceStart < 0)
            {
                return null;
            }

            jsonFenceStart = content.IndexOf('\n', jsonFenceStart);
            if (jsonFenceStart < 0)
            {
                return null;
            }

            jsonFenceStart++;
            int jsonFenceEnd = content.IndexOf("```", jsonFenceStart, StringComparison.Ordinal);
            if (jsonFenceEnd < 0)
            {
                return null;
            }

            string json = content.Substring(jsonFenceStart, jsonFenceEnd - jsonFenceStart).Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<GalleryItemState>));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(ms) as List<GalleryItemState>;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to parse saved state block: {ex.Message}");
                return null;
            }
        }

        private string SerializeGalleryStates(List<GalleryItemState> states)
        {
            var serializer = new DataContractJsonSerializer(typeof(List<GalleryItemState>));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, states);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private GalleryItemState CreateGalleryItemState(GalleryItem item, int index)
        {
            return new GalleryItemState
            {
                OriginalIndex = item.OriginalIndex >= 0 ? item.OriginalIndex : index,
                IsChecked = item.IsChecked,
                IsDuplicate = item.IsDuplicate,
                HasNoChapters = item.HasNoChapters,
                Name = item.Name,
                Link = item.Link,
                LinkCount = item.LinkCount,
                SourceDomain = item.SourceDomain,
                TotalChapters = item.TotalChapters,
                CompletedChapters = item.CompletedChapters,
                Status = item.Status,
                CurrentProcess = item.CurrentProcess,
                ErrorCount = item.GetUniqueErrorCount(),
                DownloadPath = item.DownloadPath,
                ProgressPercent = item.ProgressPercent,
                IsPaused = item.IsPaused,
                IsStopped = item.IsStopped,
                DownloadingChapter = item.DownloadingChapter,
                DownloadingPageProgress = item.DownloadingPageProgress,
                ConnectionCount = GetComboBoxSelectedInt(cmbConnections, 1),
                MultiDownloadCount = GetComboBoxSelectedInt(cmbMultiDownload, 2),
                Errors = item.GetUniqueErrors().Select(error => new ErrorState
                {
                    ChapterName = error.ChapterName,
                    PageNumber = error.PageNumber,
                    ErrorMessage = error.ErrorMessage,
                    ImageUrl = error.ImageUrl
                }).ToList()
            };
        }

        private GalleryItem CreateGalleryItemFromState(GalleryItemState state)
        {
            if (state == null)
            {
                return null;
            }

            string restoredProcess = NormalizeSavedProcess(state.CurrentProcess);
            if (string.IsNullOrWhiteSpace(restoredProcess) &&
                !string.IsNullOrWhiteSpace(state.DownloadingChapter) &&
                !string.IsNullOrWhiteSpace(state.DownloadingPageProgress))
            {
                restoredProcess = $"{state.DownloadingChapter} ({state.DownloadingPageProgress.ToLowerInvariant()})";
            }

            var item = new GalleryItem
            {
                OriginalIndex = state.OriginalIndex,
                IsChecked = state.IsChecked,
                IsDuplicate = state.IsDuplicate,
                HasNoChapters = state.HasNoChapters,
                Name = state.Name,
                Link = state.Link,
                LinkCount = state.LinkCount,
                SourceDomain = state.SourceDomain,
                TotalChapters = state.TotalChapters,
                CompletedChapters = state.CompletedChapters,
                Status = state.Status,
                CurrentProcess = restoredProcess,
                DownloadPath = state.DownloadPath,
                ProgressPercent = state.ProgressPercent,
                ConnectionCount = state.ConnectionCount,
                MultiDownloadCount = state.MultiDownloadCount,
                IsPaused = state.IsPaused,
                IsStopped = state.IsStopped,
                DownloadingChapter = state.DownloadingChapter,
                DownloadingPageProgress = state.DownloadingPageProgress,
                Errors = state.Errors == null
                    ? new List<ErrorDetail>()
                    : state.Errors.Select(error => new ErrorDetail
                    {
                        ChapterName = error.ChapterName,
                        PageNumber = error.PageNumber,
                        ErrorMessage = error.ErrorMessage,
                        ImageUrl = error.ImageUrl
                    }).ToList()
            };

            item.ErrorCount = item.GetUniqueErrorCount();
            return item;
        }

        private string NormalizeSavedProcess(string process)
        {
            if (string.IsNullOrWhiteSpace(process))
            {
                return string.Empty;
            }

            string trimmed = process.Trim();
            if (trimmed.Equals("Waiting...", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Starting...", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Done with errors", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Queued", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Paused", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Downloading", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return process;
        }

        private int GetComboBoxSelectedInt(ComboBox comboBox, int defaultValue)
        {
            if (comboBox == null)
            {
                return defaultValue;
            }

            if (comboBox.SelectedItem is ComboBoxItem selectedItem && int.TryParse(selectedItem.Content?.ToString(), out int value))
            {
                return value;
            }

            return defaultValue;
        }

        private void SetComboBoxSelectedInt(ComboBox comboBox, int value)
        {
            if (comboBox == null)
            {
                return;
            }

            foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if (int.TryParse(item.Content?.ToString(), out int parsed) && parsed == value)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void ApplySavedDownloadSettings(GalleryItem stateSource)
        {
            if (stateSource == null)
            {
                return;
            }

            if (stateSource.ConnectionCount > 0)
            {
                SetComboBoxSelectedInt(cmbConnections, stateSource.ConnectionCount);
            }

            if (stateSource.MultiDownloadCount > 0)
            {
                SetComboBoxSelectedInt(cmbMultiDownload, stateSource.MultiDownloadCount);
            }
        }

        private string EscapeMarkdownCell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", "<br>");
        }
    }
}
