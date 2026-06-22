using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace get_link_manga
{
    public partial class ErrorReportWindow : Window
    {
        private readonly GalleryItem _queueItem;
        private readonly MainWindow _mainWindow;
        private readonly List<ErrorDisplayItem> _displayItems;

        public ErrorReportWindow(GalleryItem queueItem, MainWindow mainWindow)
        {
            InitializeComponent();
            _queueItem = queueItem;
            _mainWindow = mainWindow;

            var uniqueErrors = queueItem.GetUniqueErrors();
            bool isVi = _mainWindow._isVietnameseUi;

            Title = isVi ? "BÁO CÁO LỖI" : "ERROR REPORT";
            lblErrorTitle.Text = isVi ? $"BÁO CÁO LỖI — {queueItem.Name}" : $"ERROR REPORT — {queueItem.Name}";
            lblErrorSubtitle.Text = isVi
                ? $"Nguồn: {queueItem.SourceDomain} | Tổng lỗi theo trang: {uniqueErrors.Count}"
                : $"Source: {queueItem.SourceDomain} | Total errors by page: {uniqueErrors.Count}";

            btnRetryFailed.Content = isVi ? "🔄 THỬ LẠI LỖI" : "🔄 RETRY FAILED";
            btnCopyErrors.Content = isVi ? "📋 SAO CHÉP LỖI" : "📋 COPY ERRORS";
            btnClose.Content = isVi ? "ĐÓNG" : "CLOSE";

            _displayItems = uniqueErrors.Select(e => CreateDisplayItem(queueItem, e, isVi)).ToList();
            dgErrors.ItemsSource = _displayItems;
        }

        private static ErrorDisplayItem CreateDisplayItem(GalleryItem queueItem, ErrorDetail error, bool isVi)
        {
            string rawMsg = error?.ErrorMessage ?? "Unknown error";
            string localizedMsg = LocalizeErrorMessage(rawMsg, isVi);

            return new ErrorDisplayItem
            {
                Icon = "❌",
                ComicName = queueItem?.Name ?? "N/A",
                ComicUrl = queueItem?.Link ?? string.Empty,
                ChapterName = error?.ChapterName ?? "N/A",
                PageNumber = error != null && !string.IsNullOrEmpty(error.PageName) ? error.PageName : (error?.PageNumber ?? 0).ToString(),
                ErrorMessage = localizedMsg,
                ImageUrl = error?.ImageUrl,
                ChapterUrl = error?.ChapterUrl
            };
        }

        private static string LocalizeErrorMessage(string rawMsg, bool isVi)
        {
            if (isVi)
            {
                if (rawMsg.Contains("Missing page"))
                    return "Trang bị thiếu";
                if (rawMsg.Contains("Image corrupt or too small"))
                    return "File ảnh hỏng hoặc quá nhỏ";
                return rawMsg;
            }

            if (rawMsg.Contains("Trang bị thiếu (Missing page)"))
                return "Missing page";
            if (rawMsg.Contains("File ảnh hỏng hoặc quá nhỏ"))
                return "Image corrupt or too small";
            return rawMsg;
        }

        private void BtnOpenComicLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                string url = fe.Tag as string;
                if (string.IsNullOrWhiteSpace(url) && fe.DataContext is ErrorDisplayItem item)
                {
                    url = item.ComicUrl;
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnRetryFailed_Click(object sender, RoutedEventArgs e)
        {
            bool isVi = _mainWindow._isVietnameseUi;
            if (_queueItem == null || !_queueItem.GetUniqueErrors().Any())
            {
                MessageBox.Show(
                    isVi ? "Không có lỗi nào để thử lại." : "No errors to retry.",
                    isVi ? "Thông tin" : "Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Close();
            await _mainWindow.RetryDownloadQueueItemErrorsAsync(_queueItem);
        }

        private void BtnCopyErrors_Click(object sender, RoutedEventArgs e)
        {
            bool isVi = _mainWindow._isVietnameseUi;
            if (_displayItems.Count == 0)
            {
                MessageBox.Show(
                    isVi ? "Không có lỗi nào để sao chép." : "No errors to copy.",
                    isVi ? "Thông tin" : "Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(isVi ? $"Báo cáo lỗi — {_queueItem.Name}" : $"Error Report — {_queueItem.Name}");
            sb.AppendLine(isVi ? $"Nguồn: {_queueItem.Link}" : $"Source: {_queueItem.Link}");
            sb.AppendLine(isVi ? $"Tổng số lỗi: {_displayItems.Count}" : $"Total errors: {_displayItems.Count}");
            sb.AppendLine(new string('-', 60));

            foreach (var error in _displayItems)
            {
                sb.AppendLine(isVi
                    ? $"❌ {error.ComicName} | {error.ChapterName}, Trang {error.PageNumber} — {error.ErrorMessage}"
                    : $"❌ {error.ComicName} | {error.ChapterName}, Page {error.PageNumber} — {error.ErrorMessage}");

                if (!string.IsNullOrWhiteSpace(error.ComicUrl))
                {
                    sb.AppendLine($"   Comic: {error.ComicUrl}");
                }

                if (!string.IsNullOrWhiteSpace(error.ImageUrl))
                {
                    sb.AppendLine($"   URL: {error.ImageUrl}");
                }
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show(
                isVi ? $"Đã sao chép {_displayItems.Count} lỗi vào clipboard." : $"Copied {_displayItems.Count} errors to clipboard.",
                isVi ? "Đã sao chép" : "Copied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                bool isChecked = chk.IsChecked ?? false;
                foreach (var item in _displayItems)
                {
                    item.IsChecked = isChecked;
                }
            }
        }

        private void DgErrors_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space)
            {
                if (dgErrors.SelectedItems.Count > 0)
                {
                    var firstItem = dgErrors.SelectedItems.Cast<ErrorDisplayItem>().FirstOrDefault();
                    if (firstItem != null)
                    {
                        bool targetState = !firstItem.IsChecked;
                        foreach (var item in dgErrors.SelectedItems.Cast<ErrorDisplayItem>())
                        {
                            item.IsChecked = targetState;
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        private void MenuCheckSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgErrors.SelectedItems.Cast<ErrorDisplayItem>())
            {
                item.IsChecked = true;
            }
        }

        private void MenuUncheckSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgErrors.SelectedItems.Cast<ErrorDisplayItem>())
            {
                item.IsChecked = false;
            }
        }

        private void MenuInvertChecked_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _displayItems)
            {
                item.IsChecked = !item.IsChecked;
            }
        }

        private void MenuOpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgErrors.SelectedItems.Cast<ErrorDisplayItem>())
            {
                string url = !string.IsNullOrEmpty(item.ComicUrl) ? item.ComicUrl : item.ImageUrl;
                if (!string.IsNullOrEmpty(url))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
        }

        private void MenuCopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (dgErrors.SelectedItems.Count == 0) return;
            bool isVi = _mainWindow._isVietnameseUi;
            var sb = new StringBuilder();
            foreach (var error in dgErrors.SelectedItems.Cast<ErrorDisplayItem>())
            {
                sb.AppendLine(isVi
                    ? $"❌ {error.ComicName} | {error.ChapterName}, Trang {error.PageNumber} — {error.ErrorMessage}"
                    : $"❌ {error.ComicName} | {error.ChapterName}, Page {error.PageNumber} — {error.ErrorMessage}");

                if (!string.IsNullOrWhiteSpace(error.ComicUrl))
                {
                    sb.AppendLine($"   Comic: {error.ComicUrl}");
                }
            }
            Clipboard.SetText(sb.ToString());
        }

        private void MenuDeleteErrorsAndBooks_Click(object sender, RoutedEventArgs e)
        {
            if (dgErrors.SelectedItems.Count == 0) return;

            var itemsToRemove = dgErrors.SelectedItems.Cast<ErrorDisplayItem>().ToList();
            foreach (var item in itemsToRemove)
            {
                _mainWindow.RemoveErrorFromGlobalAndQueue(item.ComicName, item.ChapterName, item.PageNumber, item.ErrorMessage);
                _displayItems.Remove(item);
            }

            dgErrors.Items.Refresh();

            var uniqueErrors = _queueItem.GetUniqueErrors();
            bool isVi = _mainWindow._isVietnameseUi;
            lblErrorSubtitle.Text = isVi
                ? $"Nguồn: {_queueItem.SourceDomain} | Tổng lỗi theo trang: {uniqueErrors.Count}"
                : $"Source: {_queueItem.SourceDomain} | Total errors by page: {uniqueErrors.Count}";
        }
    }

    public class ErrorDisplayItem : INotifyPropertyChanged
    {
        private bool _isChecked;
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

        public string Icon { get; set; }
        public string ComicName { get; set; }
        public string ComicUrl { get; set; }
        public string ChapterName { get; set; }
        public string PageNumber { get; set; }
        public string ErrorMessage { get; set; }
        public string ImageUrl { get; set; }
        public string ChapterUrl { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
