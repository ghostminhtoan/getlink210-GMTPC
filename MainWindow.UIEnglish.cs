using System;
using System.Windows;
using System.Windows.Controls;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private sealed class UiTextPair
        {
            public UiTextPair(string english, string vietnamese, params string[] legacyValues)
            {
                English = english;
                Vietnamese = vietnamese;
                LegacyValues = legacyValues ?? Array.Empty<string>();
            }

            public string English { get; }
            public string Vietnamese { get; }
            public string[] LegacyValues { get; }
        }

        private static readonly UiTextPair[] UiTextPairs =
        {
            new UiTextPair("PARAMETERS CONFIG", "1. Chọn nguồn và dán link"),
            new UiTextPair("📜 HISTORY", "📜 Lịch sử", "ðŸ“œ HISTORY", "ðŸ“œ Lá»ŠCH Sá»¬", "Ã°Å¸â€œÅ“ LÃ¡Â»Å CH SÃ¡Â»Â¬"),
            new UiTextPair("📌 BOOKMARKS", "📌 Đánh dấu", "ðŸ“Œ BOOKMARKS", "ðŸ“Œ ÄÃNH Dáº¤U", "Ã°Å¸â€œÅ’ Ã„ÂÃƒÂNH DÃ¡ÂºÂ¤U"),
            new UiTextPair("🏠 HOMEPAGE", "🏠 Trang chủ", "ðŸ  HOMEPAGE", "ðŸ  TRANG CHá»¦"),
            new UiTextPair("DOWNLOAD MULTIPLE WITH TAG OR ARTIST", "Dán link danh sách, tag, thể loại hoặc tác giả", "Táº¢I NHIá»€U Bá»˜ THEO TAG HOáº¶C TÃC GIáº¢", "TÃ¡ÂºÂ¢I NHIÃ¡Â»â‚¬U BÃ¡Â»Ëœ THEO TAG HOÃ¡ÂºÂ¶C TÃƒÂC GIÃ¡ÂºÂ¢"),
            new UiTextPair("TARGET TAG URL", "Link mục tiêu", "LINK TAG Má»¤C TIÃŠU"),
            new UiTextPair("ANALYZE TARGET PAGE", "1) Kiểm tra link", "PHÃ‚N TÃCH TRANG ÄÃCH", "PHÃƒâ€šN TÃƒÂCH TRANG Ã„ÂÃƒÂCH"),
            new UiTextPair("DELETE COOKIE", "Xóa cookie", "XÃ“A COOKIE", "XÃƒâ€œA COOKIE"),
            new UiTextPair("TOTAL PAGES", "Tổng số trang tìm thấy", "Tá»”NG Sá» TRANG", "TÃ¡Â»â€NG SÃ¡Â»Â TRANG"),
            new UiTextPair("FROM PAGE", "Lấy từ trang", "Tá»ª TRANG"),
            new UiTextPair("TO PAGE", "Đến trang", "Äáº¾N TRANG"),
            new UiTextPair("GET LINK", "2) Lấy danh sách truyện", "Láº¤Y LINK", "LÃ¡ÂºÂ¤Y LINK"),
            new UiTextPair("GET MORE", "Lấy thêm trang", "Láº¤Y THÃŠM", "LÃ¡ÂºÂ¤Y THÃƒÅ M"),
            new UiTextPair("PASTE DIRECT LINK", "Dán link truyện trực tiếp", "DÃN LINK TRá»°C TIáº¾P", "DÃƒÂN LINK TRÃ¡Â»Â°C TIÃ¡ÂºÂ¾P"),
            new UiTextPair("START CRAWLING", "Bắt đầu cào", "Báº®T Äáº¦U CÃ€O"),
            new UiTextPair("CRAWL MORE", "Cào thêm", "CÃ€O THÃŠM"),
            new UiTextPair("SORT OPTION", "Tùy chọn sắp xếp", "TÃ™Y CHá»ŒN Sáº®P Xáº¾P"),
            new UiTextPair("Recent", "Mới nhất", "Má»›i nháº¥t"),
            new UiTextPair("Popular Today", "Phổ biến hôm nay", "Phá»• biáº¿n hÃ´m nay"),
            new UiTextPair("Popular Week", "Phổ biến tuần này", "Phá»• biáº¿n tuáº§n nÃ y"),
            new UiTextPair("Popular All Time", "Phổ biến mọi lúc", "Phá»• biáº¿n má»i lÃºc"),
            new UiTextPair("TARGET PAGE ANALYSIS", "Phân tích trang đích", "PHÃ‚N TÃCH TRANG ÄÃCH"),
            new UiTextPair("Total Pages Found:", "Tổng số trang tìm thấy:", "Tá»•ng sá»‘ trang tÃ¬m tháº¥y:"),
            new UiTextPair("EXTRACTED GALLERY LINKS", "2. Danh sách chờ tải", "DANH SÃCH LINK ÄÃƒ Láº¤Y", "DANH SÃƒÂCH LINK Ã„ÂÃƒÆ’ LÃ¡ÂºÂ¤Y"),
            new UiTextPair("GALLERY DETAILS (TITLE & URL)", "Chi tiết truyện (tên & URL)", "CHI TIáº¾T TRUYá»†N (TÃŠN & URL)", "CHI TIÃ¡ÂºÂ¾T TRUYÃ¡Â»â€ N (TÃƒÅ N & URL)"),
            new UiTextPair("STATUS", "Trạng thái", "TRáº NG THÃI", "TRÃ¡ÂºÂ NG THÃƒÂI"),
            new UiTextPair("PROCESS", "Tiến trình", "TIáº¾N TRÃŒNH", "TIÃ¡ÂºÂ¾N TRÃƒÅ’NH"),
            new UiTextPair("VIEW LINK", "Mở nhanh", "XEM LINK"),
            new UiTextPair("COPY LINKS", "Sao chép link"),
            new UiTextPair("COPY NAME+LINK", "Sao chép tên + link"),
            new UiTextPair("COPY NAME + LINK", "Sao chép tên + link", "COPY TÃŠN + LINK", "COPY TÃƒÅ N + LINK"),
            new UiTextPair("SAVE LIST", "Lưu danh sách", "LÆ¯U DANH SÃCH", "LÃ†Â¯U DANH SÃƒÂCH"),
            new UiTextPair("LOAD LIST", "Tải danh sách", "Táº¢I DANH SÃCH", "TÃ¡ÂºÂ¢I DANH SÃƒÂCH"),
            new UiTextPair("CLEAR", "Xóa", "XÃ“A", "XÃƒâ€œA"),
            new UiTextPair("WORD WRAP", "Xuống dòng", "XUá»NG DÃ’NG", "XUÃ¡Â»ÂNG DÃƒâ€™NG"),
            new UiTextPair("SEARCH", "Tìm kiếm", "TÃŒM KIáº¾M", "TÃƒÅ’M KIÃ¡ÂºÂ¾M"),
            new UiTextPair("FOLDER ACTIONS", "Công cụ thư mục", "THAO TÃC THÆ¯ Má»¤C", "THAO TÃƒÂC THÃ†Â¯ MÃ¡Â»Â¤C"),
            new UiTextPair("CHAPTER SELECTION", "Chỉ tải chapter", "CHá»ŒN CHAPTER", "CHÃ¡Â»Å’N CHAPTER"),
            new UiTextPair("CONNECTION", "Kết nối", "Káº¾T Ná»I", "KÃ¡ÂºÂ¾T NÃ¡Â»ÂI"),
            new UiTextPair("DOWNLOAD MULTIPLE BOOK", "Tải cùng lúc", "Táº¢I NHIá»€U TRUYá»†N", "TÃ¡ÂºÂ¢I NHIÃ¡Â»â‚¬U TRUYÃ¡Â»â€ N"),
            new UiTextPair("SORT BY NAME", "Sắp xếp theo tên", "Sáº®P Xáº¾P THEO TÃŠN", "SÃ¡ÂºÂ®P XÃ¡ÂºÂ¾P THEO TÃƒÅ N"),
            new UiTextPair("RESTORE ORDER", "Trả về thứ tự cũ", "KHÃ”I PHá»¤C THá»¨ Tá»°", "KHÃƒâ€I PHÃ¡Â»Â¤C THÃ¡Â»Â¨ TÃ¡Â»Â°"),
            new UiTextPair("DUPLICATE NAME", "Tên bị trùng", "TRÃ™NG TÃŠN", "TRÃƒâ„¢NG TÃƒÅ N"),
            new UiTextPair("NO LINK (VI-HENTAI)", "Không link (VI-HENTAI)", "KHÃ”NG LINK (VI-HENTAI)", "KHÃƒâ€NG LINK (VI-HENTAI)"),
            new UiTextPair("REVERSE ORDER", "Đảo chiều", "Äáº¢O CHIá»€U", "Ã„ÂÃ¡ÂºÂ¢O CHIÃ¡Â»â‚¬U"),
            new UiTextPair("CLEAR COMPLETE BOOKS", "Ẩn truyện đã tải xong", "XÃ“A TRUYá»†N HOÃ€N Táº¤T", "XÃƒâ€œA TRUYÃ¡Â»â€ N HOÃƒâ‚¬N TÃ¡ÂºÂ¤T"),
            new UiTextPair("BROWSE", "Duyệt", "DUYá»†T"),
            new UiTextPair("OPEN FOLDER", "Mở thư mục", "Má»ž THÆ¯ Má»¤C"),
            new UiTextPair("MERGE", "Gộp", "Gá»˜P"),
            new UiTextPair("SPLIT", "Tách", "TÃCH"),
            new UiTextPair("COMPRESS", "Nén", "NÃ‰N"),
            new UiTextPair("EXTRACT", "Giải nén", "GIáº¢I NÃ‰N"),
            new UiTextPair("START", "Tải"),
            new UiTextPair("STOP", "Dừng"),
            new UiTextPair("RETRY", "Thử lại"),
            new UiTextPair("LOG", "Nhật ký"),
            new UiTextPair("DOWNLOAD SETTINGS", "3. Thiết lập tải"),
            new UiTextPair("QUICK ACTIONS FOR CURRENT LIST", "Thao tác nhanh cho danh sách hiện tại"),
            new UiTextPair("Simple flow: choose website, paste link, review list, then download.", "Cách dùng nhanh: chọn web, dán link, xem danh sách rồi tải."),
            new UiTextPair("CHOOSE SOURCE", "CHỌN NGUỒN"),
            new UiTextPair("PASTE LINK", "DÁN LINK"),
            new UiTextPair("DOWNLOAD", "TẢI VỀ"),
            new UiTextPair("STEP 1", "BƯỚC 1"),
            new UiTextPair("STEP 2", "BƯỚC 2"),
            new UiTextPair("STEP 3", "BƯỚC 3"),
            new UiTextPair("INTERFACE LANGUAGE", "NGÔN NGỮ GIAO DIỆN"),
            new UiTextPair("DISPLAY SCALE", "TỶ LỆ HIỂN THỊ"),
            new UiTextPair("100% = baseline cho 1360x768", "100% = chuẩn cho 1360x768"),
            new UiTextPair("Review queue, tick what you want, then start download.", "Kiểm tra danh sách, tích mục muốn tải, rồi bấm tải."),
            new UiTextPair("Check selected rows", "Tích chọn dòng đang bôi đen"),
            new UiTextPair("Uncheck selected rows", "Bỏ tích dòng đang bôi đen"),
            new UiTextPair("Invert checked state", "Đảo ngược trạng thái tích"),
            new UiTextPair("📌 Bookmark selected row", "📌 Đánh dấu dòng đang chọn", "ðŸ“Œ Bookmark selected row", "ðŸ“Œ ÄÃ¡nh dáº¥u dÃ²ng Ä‘ang chá»n"),
            new UiTextPair("🌐 Open link in browser", "🌐 Mở link trong trình duyệt", "ðŸŒ Open link in browser", "ðŸŒ Má»Ÿ link trong trÃ¬nh duyá»‡t"),
            new UiTextPair("Copy selected links", "Copy link các dòng đang chọn", "Copy link cÃ¡c dÃ²ng Ä‘ang chá»n"),
            new UiTextPair("Delete selected rows", "Xóa các dòng đang bôi đen", "XÃ³a cÃ¡c dÃ²ng Ä‘ang bÃ´i Ä‘en"),
            new UiTextPair("Delete checked rows", "Xóa các dòng đã tích", "XÃ³a cÃ¡c dÃ²ng Ä‘Ã£ tÃ­ch"),
            new UiTextPair("Download selected rows", "Tải các dòng đang bôi đen", "Táº£i cÃ¡c dÃ²ng Ä‘ang bÃ´i Ä‘en"),
            new UiTextPair("Download checked rows", "Tải các dòng đã tích", "Táº£i cÃ¡c dÃ²ng Ä‘Ã£ tÃ­ch"),
            new UiTextPair("Tích chọn dòng đang bôi đen (Check Selected)", "Tích chọn dòng đang bôi đen", "TÃ­ch chá»n dÃ²ng Ä‘ang bÃ´i Ä‘en (Check Selected)"),
            new UiTextPair("Bỏ tích dòng đang bôi đen (Uncheck Selected)", "Bỏ tích dòng đang bôi đen", "Bá» tÃ­ch dÃ²ng Ä‘ang bÃ´i Ä‘en (Uncheck Selected)"),
            new UiTextPair("Đảo ngược trạng thái tích (Invert Checked)", "Đảo ngược trạng thái tích", "Äáº£o ngÆ°á»£c tráº¡ng thÃ¡i tÃ­ch (Invert Checked)"),
            new UiTextPair("📌 Bookmark dòng đang chọn", "📌 Đánh dấu dòng đang chọn", "ðŸ“Œ Bookmark dÃ²ng Ä‘ang chá»n"),
            new UiTextPair("🌐 Mở link trong trình duyệt", "🌐 Mở link trong trình duyệt", "ðŸŒ Má»Ÿ link trong trÃ¬nh duyá»‡t"),
            new UiTextPair("Copy link các dòng đang chọn (Copy Selected Links)", "Copy link các dòng đang chọn", "Copy link cÃ¡c dÃ²ng Ä‘ang chá»n (Copy Selected Links)"),
            new UiTextPair("Xóa các dòng đang bôi đen (Delete Selected)", "Xóa các dòng đang bôi đen", "XÃ³a cÃ¡c dÃ²ng Ä‘ang bÃ´i Ä‘en (Delete Selected)"),
            new UiTextPair("Xóa các dòng đã tích (Delete Checked)", "Xóa các dòng đã tích", "XÃ³a cÃ¡c dÃ²ng Ä‘Ã£ tÃ­ch (Delete Checked)"),
            new UiTextPair("Tải các dòng đang bôi đen (Download highlighted lines)", "Tải các dòng đang bôi đen", "Táº£i cÃ¡c dÃ²ng Ä‘ang bÃ´i Ä‘en (Download highlighted lines)"),
            new UiTextPair("Tải các dòng đã tích (Download selected lines)", "Tải các dòng đã tích", "Táº£i cÃ¡c dÃ²ng Ä‘Ã£ tÃ­ch (Download selected lines)")
        };

        internal bool _isVietnameseUi;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyCurrentUiLanguage();
            UpdateTruyenqqSpecificActions();
            PromptExtractPortableArchiveOnStartup();
        }

        private void PromptExtractPortableArchiveOnStartup()
        {
            if (_startupArchivePromptShown)
            {
                return;
            }

            _startupArchivePromptShown = true;

            string archivePath = PortablePaths.PortableArchivePath;
            if (!System.IO.File.Exists(archivePath))
            {
                return;
            }

            string message =
                "Found archive file Comic-GMTPC.zip.\n" +
                "Tìm thấy file nén Comic-GMTPC.zip.\n\n" +
                "Do you want to extract it now?\n" +
                "Bạn có muốn giải nén ngay bây giờ không?";

            string title = "Extract archive / Giải nén file nén";

            if (MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                BtnExtractBooks_Click(this, new RoutedEventArgs());
            }
        }

        private void TglLanguage_Checked(object sender, RoutedEventArgs e)
        {
            _isVietnameseUi = true;
            ApplyCurrentUiLanguage();
        }

        private void TglLanguage_Unchecked(object sender, RoutedEventArgs e)
        {
            _isVietnameseUi = false;
            ApplyCurrentUiLanguage();
        }

        private void TabManga_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTruyenqqSpecificActions();
        }

        private void ApplyCurrentUiLanguage()
        {
            Application.Current.Properties["IsVietnameseUi"] = _isVietnameseUi;

            if (_isVietnameseUi)
            {
                ApplyVietnameseUiText();
            }
            else
            {
                ApplyEnglishUiText();
            }

            UpdateTruyenqqSpecificActions();
            _bookmarkHistoryWindowInstance?.ApplyLanguage(_isVietnameseUi);
            RefreshVisibleGalleryLanguage();
            UpdateFoldButtonUi();
            UpdateWorkspaceShellLanguage();
        }

        private void RefreshVisibleGalleryLanguage()
        {
            foreach (GalleryItem item in _scrapedItems)
            {
                item?.RefreshDisplayText();
            }
        }

        private void ApplyResultsGridHeaderLanguage(
            string galleryDetailsText,
            string statusText,
            string processText,
            string viewText)
        {
            SetResultsColumnHeaderText(colGalleryDetails, galleryDetailsText);
            SetResultsColumnHeaderText(colStatus, statusText);
            SetResultsColumnHeaderText(colProcess, processText);

            if (colViewLink != null)
            {
                colViewLink.Header = viewText;
            }
        }

        private void SetResultsColumnHeaderText(System.Windows.Controls.DataGridColumn column, string text)
        {
            if (column == null)
            {
                return;
            }

            if (column.Header is TextBlock textBlock)
            {
                textBlock.Text = text;
                return;
            }

            if (column.Header is DependencyObject dependencyObject)
            {
                TextBlock nestedTextBlock = FindHeaderTextBlock(dependencyObject);
                if (nestedTextBlock != null)
                {
                    nestedTextBlock.Text = text;
                    return;
                }
            }

            column.Header = text;
        }

        private TextBlock FindHeaderTextBlock(DependencyObject root)
        {
            if (root == null)
            {
                return null;
            }

            if (root is TextBlock textBlock)
            {
                return textBlock;
            }

            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyChild)
                {
                    TextBlock nested = FindHeaderTextBlock(dependencyChild);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }

            return null;
        }

        private void UpdateTruyenqqSpecificActions()
        {
            if (btnSortByLatestChapter == null)
            {
                return;
            }

            bool isVisible =
                tabMangaRootItem != null && tabMangaRootItem.IsSelected &&
                tabTruyenqqItem != null && tabTruyenqqItem.IsSelected;

            btnSortByLatestChapter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateLatestChapterButtonLabel()
        {
            if (btnSortByLatestChapter == null)
            {
                return;
            }

            string suffix = _lastLatestChapterSortDescending == true
                ? " ↓"
                : _lastLatestChapterSortDescending == false
                    ? " ↑"
                    : string.Empty;

            btnSortByLatestChapter.Content = _isVietnameseUi
                ? $"CHƯƠNG MỚI NHẤT{suffix}"
                : $"LATEST CHAPTER{suffix}";
        }

        private void ApplyEnglishUiText()
        {
            Title = "Comic-GMTPC v1.0 - English";

            if (txtHeaderTitle != null) txtHeaderTitle.Text = string.Empty;
            if (txtHeaderSubtitle != null) txtHeaderSubtitle.Text = string.Empty;
            if (txtHeaderStepPrimary != null) txtHeaderStepPrimary.Text = "STEP 1";
            if (txtHeaderStepPrimaryTitle != null) txtHeaderStepPrimaryTitle.Text = "CHOOSE SOURCE OR PASTE LINK";
            if (txtHeaderStepSecondary != null) txtHeaderStepSecondary.Text = "STEP 2";
            if (txtHeaderStepSecondaryTitle != null) txtHeaderStepSecondaryTitle.Text = "DOWNLOAD";
            if (txtLanguageLabel != null) txtLanguageLabel.Text = "ENG";
            if (txtLanguageTarget != null) txtLanguageTarget.Text = "VI";
            if (txtTotalBooksLabel != null) txtTotalBooksLabel.Text = "Total titles: ";
            if (txtResultsHeader != null) txtResultsHeader.Text = "EXTRACTED GALLERY LINKS";
            if (btnShutdownMenu != null) btnShutdownMenu.ToolTip = "Shutdown options";
            if (txtShutdownPopupHeader != null) txtShutdownPopupHeader.Text = "SHUTDOWN OPTIONS";
            if (chkShutdownAfterCompleted != null) chkShutdownAfterCompleted.Content = "shutdown after completed";
            if (txtShutdownCountdownLabel != null) txtShutdownCountdownLabel.Text = "shutdown in day:hour:minute:second";
            if (txtShutdownDaysLabel != null) txtShutdownDaysLabel.Text = "DAY";
            if (txtShutdownHoursLabel != null) txtShutdownHoursLabel.Text = "HOUR";
            if (txtShutdownMinutesLabel != null) txtShutdownMinutesLabel.Text = "MIN";
            if (txtShutdownSecondsLabel != null) txtShutdownSecondsLabel.Text = "SEC";
            if (txtShutdownPopupHint != null) txtShutdownPopupHint.Text = "0:1:30:0 = 1 hour 30 minutes. Schedule uses Windows shutdown command.";
            if (btnScheduleShutdownTimer != null) btnScheduleShutdownTimer.Content = "SCHEDULE";
            if (btnCancelShutdownTimer != null) btnCancelShutdownTimer.Content = "CANCEL";
            if (btnCloseShutdownPopup != null) btnCloseShutdownPopup.Content = "CLOSE";

            ApplyUiTextMappings(false);
            ApplyResultsGridHeaderLanguage("GALLERY DETAILS", "STATUS", "PROCESS", "VIEW");

            if (btnSaveList != null) btnSaveList.Content = "Save default";
            if (btnLoadList != null) btnLoadList.Content = "Open default";
            if (btnNewList != null) btnNewList.Content = "New list";
            if (btnSaveCustomList != null) btnSaveCustomList.Content = "Save as";
            if (btnLoadCustomList != null) btnLoadCustomList.Content = "Open file";
            if (btnRestoreOrder != null) btnRestoreOrder.Content = "Original order";
            if (btnDuplicateName != null) btnDuplicateName.Content = "Duplicate names";
            if (btnNoLinkViHentai != null) btnNoLinkViHentai.Content = "No chapters";
            if (btnReverseOrder != null) btnReverseOrder.Content = "Reverse order";
            if (btnClearComplete != null) btnClearComplete.Content = "Hide completed";

            UpdateLatestChapterButtonLabel();
        }

        private void ApplyUiTextMappings(bool vietnamese)
        {
            ApplyUiTextMappingsRecursive(this, vietnamese);

            if (dgResults?.ContextMenu != null)
            {
                ApplyUiTextMappingsRecursive(dgResults.ContextMenu, vietnamese);
            }
        }

        private void ApplyUiTextMappingsRecursive(object node, bool vietnamese)
        {
            if (node == null)
            {
                return;
            }

            if (node is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
            {
                textBlock.Text = TranslateUiText(textBlock.Text, vietnamese);
            }
            else if (node is Button button && button.Content is string buttonText)
            {
                button.Content = TranslateUiText(buttonText, vietnamese);
            }
            else if (node is MenuItem menuItem && menuItem.Header is string headerText)
            {
                menuItem.Header = TranslateUiText(headerText, vietnamese);
            }
            else if (node is TabItem tabItem && tabItem.Header is string tabHeader)
            {
                tabItem.Header = TranslateUiText(tabHeader, vietnamese);
            }
            else if (node is ComboBoxItem comboBoxItem && comboBoxItem.Content is string comboBoxText)
            {
                comboBoxItem.Content = TranslateUiText(comboBoxText, vietnamese);
            }

            if (node is ItemsControl itemsControl)
            {
                foreach (object item in itemsControl.Items)
                {
                    ApplyUiTextMappingsRecursive(item, vietnamese);
                }
            }

            if (node is DependencyObject dependencyObject)
            {
                foreach (object child in LogicalTreeHelper.GetChildren(dependencyObject))
                {
                    ApplyUiTextMappingsRecursive(child, vietnamese);
                }
            }
        }

        private string TranslateUiText(string currentText, bool vietnamese)
        {
            foreach (UiTextPair pair in UiTextPairs)
            {
                if (MatchesUiText(currentText, pair))
                {
                    return vietnamese ? pair.Vietnamese : pair.English;
                }
            }

            return currentText;
        }

        private bool MatchesUiText(string currentText, UiTextPair pair)
        {
            if (UiTextEquals(currentText, pair.English) || UiTextEquals(currentText, pair.Vietnamese))
            {
                return true;
            }

            foreach (string legacyValue in pair.LegacyValues)
            {
                if (UiTextEquals(currentText, legacyValue))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool UiTextEquals(string left, string right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
        }
    }
}
