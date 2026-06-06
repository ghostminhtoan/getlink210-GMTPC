using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private readonly SemaphoreSlim _folderStructureSemaphore = new SemaphoreSlim(1, 1);

        private async void BtnMergeFolders_Click(object sender, RoutedEventArgs e)
        {
            string downloadRoot = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(downloadRoot))
            {
                MessageBox.Show("Vui lÃ²ng chá»n thÆ° má»¥c lÆ°u trÆ°á»›c (Please select a download folder first).", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(downloadRoot))
            {
                MessageBox.Show("ThÆ° má»¥c lÆ°u khÃ´ng tá»“n táº¡i (Download folder does not exist).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string targetFolder = GetActiveTargetFolder(downloadRoot);

            Log($"[Merge] Báº¯t Ä‘áº§u gá»™p thÆ° má»¥c táº¡i: {targetFolder}");
            lblStatus.Text = "Merging folders...";

            try
            {
                int mergedCount = await MergeFoldersInTargetFolderAsync(targetFolder, CancellationToken.None);

                Log("[Merge] Äang táº¡m ngá»«ng 3 giÃ¢y Ä‘á»ƒ há»‡ thá»‘ng á»•n Ä‘á»‹nh vÃ  nháº­n biáº¿t thÆ° má»¥c...");
                await Task.Delay(3000);

                if (mergedCount == 0)
                {
                    Log("[Merge] KhÃ´ng tÃ¬m tháº¥y nhÃ³m thÆ° má»¥c nÃ o cÃ³ tÃªn giá»‘ng nhau trÆ°á»›c dáº¥u gáº¡ch ná»‘i.");
                    lblStatus.Text = "Merge completed. No folders merged.";
                    MessageBox.Show("KhÃ´ng tÃ¬m tháº¥y thÆ° má»¥c nÃ o cÃ³ tÃªn giá»‘ng nhau trÆ°á»›c dáº¥u gáº¡ch ná»‘i Ä‘á»ƒ gá»™p (No matching folders found to merge).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Log($"[Merge] HoÃ n táº¥t gá»™p {mergedCount} thÆ° má»¥c.");
                lblStatus.Text = $"Merge completed. Merged {mergedCount} folders.";
                MessageBox.Show($"ÄÃ£ gá»™p thÃ nh cÃ´ng {mergedCount} thÆ° má»¥c!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Merge Error] Lá»—i nghiÃªm trá»ng khi gá»™p thÆ° má»¥c: {ex.Message}");
                lblStatus.Text = "Merge failed.";
                MessageBox.Show($"Lá»—i khi gá»™p thÆ° má»¥c: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSplitFolders_Click(object sender, RoutedEventArgs e)
        {
            string downloadRoot = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(downloadRoot))
            {
                MessageBox.Show("Vui lÃ²ng chá»n thÆ° má»¥c lÆ°u trÆ°á»›c (Please select a download folder first).", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(downloadRoot))
            {
                MessageBox.Show("ThÆ° má»¥c lÆ°u khÃ´ng tá»“n táº¡i (Download folder does not exist).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string targetFolder = GetActiveTargetFolder(downloadRoot);

            Log($"[Split] Báº¯t Ä‘áº§u tÃ¡ch thÆ° má»¥c táº¡i: {targetFolder}");
            lblStatus.Text = "Splitting folders...";

            try
            {
                int splitCount = await SplitFoldersInTargetFolderAsync(targetFolder, CancellationToken.None);

                Log("[Split] Äang táº¡m ngá»«ng 3 giÃ¢y Ä‘á»ƒ há»‡ thá»‘ng á»•n Ä‘á»‹nh vÃ  nháº­n biáº¿t thÆ° má»¥c...");
                await Task.Delay(3000);

                Log($"[Split] HoÃ n táº¥t tÃ¡ch {splitCount} thÆ° má»¥c.");
                lblStatus.Text = $"Split completed. Split {splitCount} folders.";
                MessageBox.Show($"ÄÃ£ tÃ¡ch thÃ nh cÃ´ng {splitCount} thÆ° má»¥c!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Split Error] Lá»—i nghiÃªm trá»ng khi tÃ¡ch thÆ° má»¥c: {ex.Message}");
                lblStatus.Text = "Split failed.";
                MessageBox.Show($"Lá»—i khi tÃ¡ch thÆ° má»¥c: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal async Task AutoMergeChapterFolderAsync(string unmergedPath, string mergedPath, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(unmergedPath) ||
                string.IsNullOrWhiteSpace(mergedPath) ||
                string.Equals(unmergedPath, mergedPath, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(unmergedPath))
            {
                return;
            }

            await _folderStructureSemaphore.WaitAsync(token);
            try
            {
                string mergedParent = Path.GetDirectoryName(mergedPath);
                if (string.IsNullOrEmpty(mergedParent))
                {
                    return;
                }

                Directory.CreateDirectory(mergedParent);
                if (Directory.Exists(mergedPath))
                {
                    MergeDirectoryContents(unmergedPath, mergedPath);
                }
                else
                {
                    Directory.Move(unmergedPath, mergedPath);
                }

                Log($"[Auto Merge] ÄÃ£ gá»™p tá»± Ä‘á»™ng '{Path.GetFileName(unmergedPath)}' -> '{mergedPath}'");
            }
            finally
            {
                _folderStructureSemaphore.Release();
            }
        }

        private async Task<int> MergeFoldersInTargetFolderAsync(string targetFolder, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            {
                return 0;
            }

            await _folderStructureSemaphore.WaitAsync(token);
            try
            {
                var directories = Directory.GetDirectories(targetFolder);
                var groups = directories
                    .Select(dir => new { Path = dir, Name = Path.GetFileName(dir) })
                    .Where(d => !ShouldIgnoreFolderStructureAction(d.Name))
                    .Where(d => d.Name.Contains("-"))
                    .Select(d =>
                    {
                        int index = d.Name.IndexOf('-');
                        string prefix = d.Name.Substring(0, index).Trim();
                        string suffix = d.Name.Substring(index + 1).Trim();
                        return new { d.Path, d.Name, Prefix = prefix, Suffix = suffix };
                    })
                    .GroupBy(d => d.Prefix, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() >= 2)
                    .ToList();

                int mergedCount = 0;
                foreach (var group in groups)
                {
                    string groupPrefix = group.Key;
                    string destParentDir = Path.Combine(targetFolder, groupPrefix);
                    Directory.CreateDirectory(destParentDir);

                    foreach (var item in group)
                    {
                        string destDir = Path.Combine(destParentDir, item.Suffix);

                        try
                        {
                            if (!Directory.Exists(destDir))
                            {
                                Directory.Move(item.Path, destDir);
                            }
                            else
                            {
                                MergeDirectoryContents(item.Path, destDir);
                            }

                            mergedCount++;
                            Log($"[Merge] ÄÃ£ gá»™p '{item.Name}' -> '{groupPrefix}\\{item.Suffix}'");
                        }
                        catch (Exception ex)
                        {
                            Log($"[Merge Error] KhÃ´ng thá»ƒ gá»™p thÆ° má»¥c '{item.Name}': {ex.Message}");
                        }
                    }
                }

                return mergedCount;
            }
            finally
            {
                _folderStructureSemaphore.Release();
            }
        }

        private async Task<int> SplitFoldersInTargetFolderAsync(string targetFolder, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            {
                return 0;
            }

            await _folderStructureSemaphore.WaitAsync(token);
            try
            {
                var directories = Directory.GetDirectories(targetFolder);
                int splitCount = 0;

                foreach (var dir in directories)
                {
                    string parentName = Path.GetFileName(dir);
                    if (ShouldIgnoreFolderStructureAction(parentName))
                    {
                        continue;
                    }

                    var subDirs = Directory.GetDirectories(dir);
                    if (subDirs.Length < 2)
                    {
                        continue;
                    }

                    foreach (var subDir in subDirs)
                    {
                        string subName = Path.GetFileName(subDir);
                        string destName = $"{parentName}-{subName}";
                        string destPath = Path.Combine(targetFolder, destName);

                        try
                        {
                            if (!Directory.Exists(destPath))
                            {
                                Directory.Move(subDir, destPath);
                            }
                            else
                            {
                                MergeDirectoryContents(subDir, destPath);
                            }

                            splitCount++;
                            Log($"[Split] ÄÃ£ tÃ¡ch '{parentName}\\{subName}' -> '{destName}'");
                        }
                        catch (Exception ex)
                        {
                            Log($"[Split Error] KhÃ´ng thá»ƒ tÃ¡ch thÆ° má»¥c '{parentName}\\{subName}': {ex.Message}");
                        }
                    }

                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir, false);
                            Log($"[Split] ÄÃ£ xÃ³a thÆ° má»¥c cha trá»‘ng: '{parentName}'");
                        }
                        else
                        {
                            Log($"[Split] ThÆ° má»¥c cha '{parentName}' váº«n cÃ²n tá»‡p/thÆ° má»¥c khÃ¡c nÃªn khÃ´ng xÃ³a.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[Split Warning] KhÃ´ng thá»ƒ xÃ³a thÆ° má»¥c cha '{parentName}': {ex.Message}");
                    }
                }

                return splitCount;
            }
            finally
            {
                _folderStructureSemaphore.Release();
            }
        }

        private bool ShouldIgnoreFolderStructureAction(string folderName)
        {
            return string.IsNullOrWhiteSpace(folderName) ||
                   folderName.StartsWith(".", StringComparison.OrdinalIgnoreCase) ||
                   folderName.EndsWith("-tmp", StringComparison.OrdinalIgnoreCase);
        }

        private void MergeDirectoryContents(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(dest, Path.GetFileName(file));
                if (File.Exists(destFile))
                {
                    File.Delete(file);
                }
                else
                {
                    File.Move(file, destFile);
                }
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(dest, Path.GetFileName(dir));
                MergeDirectoryContents(dir, destDir);
            }

            Directory.Delete(source, true);
        }

        private string GetActiveTargetFolder(string downloadRoot)
        {
            string subFolder = "";
            Dispatcher.Invoke(() =>
            {
                if (tabLeftPanel == null) return;

                if (tabLeftPanel.SelectedIndex == 0)
                {
                    if (tabManga != null && tabManga.SelectedItem is System.Windows.Controls.TabItem selectedMangaTab)
                    {
                        string header = selectedMangaTab.Header?.ToString().ToLower() ?? "";
                        if (header.Contains("truyenqq"))
                            subFolder = "truyenqq";
                        else if (header.Contains("nettruyen"))
                            subFolder = "nettruyen";
                    }
                    else
                    {
                        subFolder = "truyenqq";
                    }
                }
                else if (tabLeftPanel.SelectedIndex == 1)
                {
                    if (tabHentai != null && tabHentai.SelectedItem is System.Windows.Controls.TabItem selectedHentaiTab)
                    {
                        string header = selectedHentaiTab.Header?.ToString().ToLower() ?? "";
                        if (header.Contains("hentaiforce"))
                            subFolder = "hentaiforce.net";
                        else if (header.Contains("nhentai"))
                            subFolder = "nhentai.net";
                        else if (header.Contains("hentaivn"))
                            subFolder = "vi-hentai.pro";
                        else if (header.Contains("hentaiera"))
                            subFolder = "hentaiera.com";
                    }
                }
            });

            string targetFolder = string.IsNullOrEmpty(subFolder)
                ? downloadRoot
                : Path.Combine(downloadRoot, subFolder);

            if (!Directory.Exists(targetFolder))
            {
                targetFolder = downloadRoot;
            }

            return targetFolder;
        }
    }
}
