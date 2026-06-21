using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace get_link_manga
{
    public partial class ErrorLogWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly List<ErrorDisplayItem> _displayItems;

        public ErrorLogWindow(IEnumerable<GalleryItem> erroredItems, MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;

            bool isVi = _mainWindow._isVietnameseUi;
            var items = (erroredItems ?? Enumerable.Empty<GalleryItem>()).ToList();

            Title = isVi ? "BÁO CÁO LỖI" : "ERROR LOG";
            lblLogTitle.Text = isVi ? "BÁO CÁO LỖI" : "ERROR LOG";
            lblLogSubtitle.Text = isVi
                ? $"Tổng truyện lỗi: {items.Count}"
                : $"Errored comics: {items.Count}";

            _displayItems = new List<ErrorDisplayItem>();
            foreach (var comic in items)
            {
                var errors = comic.GetUniqueErrors() ?? Enumerable.Empty<ErrorDetail>();
                var errorList = errors.ToList();
                
                bool hasNoChapters = comic.HasNoChapters || errorList.Any(e => 
                    (e.ChapterName != null && e.ChapterName.IndexOf("Không có chương", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (e.ErrorMessage != null && e.ErrorMessage.IndexOf("chưa có chương", StringComparison.OrdinalIgnoreCase) >= 0)
                );

                if (hasNoChapters)
                {
                    errorList = errorList.Where(e => 
                        (e.ChapterName != null && e.ChapterName.IndexOf("Không có chương", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (e.ErrorMessage != null && e.ErrorMessage.IndexOf("chưa có chương", StringComparison.OrdinalIgnoreCase) >= 0)
                    ).ToList();
                }

                foreach (var error in errorList)
                {
                    _displayItems.Add(new ErrorDisplayItem
                    {
                        Icon = "❌",
                        ComicName = comic.Name ?? "N/A",
                        ComicUrl = comic.Link ?? string.Empty,
                        ChapterName = error?.ChapterName ?? "N/A",
                        PageNumber = error != null && !string.IsNullOrEmpty(error.PageName) ? error.PageName : (error?.PageNumber ?? 0).ToString(),
                        ErrorMessage = LocalizeErrorMessage(error?.ErrorMessage ?? "Unknown error", isVi),
                        ImageUrl = error?.ImageUrl,
                        ChapterUrl = error?.ChapterUrl
                    });
                }
            }

            dgLogs.ItemsSource = _displayItems;
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

        private void BtnCopyLogs_Click(object sender, RoutedEventArgs e)
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
            sb.AppendLine(isVi ? "BÁO CÁO LỖI" : "ERROR LOG");
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
    }
}
