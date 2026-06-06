using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
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

            // Copy in tabular format (Name \t Link)
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
                FileName = "hentaiforce_galleries.md"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# Scraped HentaiForce Galleries");
                    sb.AppendLine();
                    sb.AppendLine("| No. | Gallery Name | Gallery Link | Selected |");
                    sb.AppendLine("| :--- | :--- | :--- | :--- |");
                    
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        // Replace pipe characters to avoid breaking Markdown tables
                        string safeName = item.Name.Replace("|", "\\|");
                        string checkedStr = item.IsChecked ? "[x]" : "[ ]";
                        sb.AppendLine($"| {i + 1} | {safeName} | {item.Link} | {checkedStr} |");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    
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
                    var lines = File.ReadAllLines(openFileDialog.FileName);
                    List<GalleryItem> loadedItems = new List<GalleryItem>();

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        // Parse markdown table rows: | 1 | [KakuseiKakusei] ... | https://... |
                        if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
                        {
                            var parts = trimmed.Split('|');
                            if (parts.Length >= 4)
                            {
                                string noStr = parts[1].Trim();
                                string name = parts[2].Trim();
                                string link = parts[3].Trim();

                                // Skip markdown table headers, dividers, and alignments
                                if (noStr.Equals("No.", StringComparison.OrdinalIgnoreCase) || 
                                    noStr.Contains("---") || 
                                    noStr.Contains(":") ||
                                    string.IsNullOrEmpty(noStr))
                                {
                                    continue;
                                }

                                if (!string.IsNullOrEmpty(link) && link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Restore original pipe characters
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
    }
}
