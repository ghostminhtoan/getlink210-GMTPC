# Hướng Dẫn Phát Triển & Quy Tắc Code - Dự Án "get link manga"

Tài liệu này chứa toàn bộ quy tắc code, cấu trúc dự án, hướng dẫn thiết kế giao diện và quy trình làm việc. **Tất cả các Agent AI (như Antigravity, Claude, Cursor, Copilot...) BẮT BUỘC phải đọc file này trước khi thực hiện bất kỳ chỉnh sửa nào.**

---

## 1. Tổng Quan Dự Án
- **Tên dự án:** get link manga
- **Loại ứng dụng:** Windows Desktop Application (WPF)
- **Framework:** .NET Framework 4.8 (C#)
- **Thư viện UI:** HandyControl (v3.5.0)
- **Chủ đề giao diện (Theme):** Cyberpunk (Tông màu tối, hiệu ứng phát sáng Neon)
- **Tính năng chính:** Cào liên kết truyện tranh từ trang HentaiForce, hiển thị danh sách kết quả, sao chép kết quả vào clipboard (chỉ link hoặc dạng bảng), lưu/đọc kết quả từ file Markdown.

---

## 2. Quy Tắc Code & Lập Trình C#
- **Kh�ng t? � kh?i t?o `HttpClient` m?i:** B?t bu?c s? d?ng instance static `_httpClient` du?c d?nh nghia s?n trong [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). N� d� du?c c?u h�nh t? d?ng gi?i n�n (GZip, Deflate) v� User-Agent ph� h?p.
- **Xử lý bất đồng bộ (Async/Await):** Luôn dùng `async/await` cho các tác vụ I/O (cào web, đọc ghi file) để tránh đơ giao diện.
- **Cơ chế dừng cào (Cancellation):** Các vòng lặp cào dữ liệu phải hỗ trợ `CancellationToken` từ `CancellationTokenSource` (`_cts`). Kiểm tra `token.IsCancellationRequested` để dừng tác vụ kịp thời và đưa nút bấm về trạng thái ban đầu trong khối `finally`.
- **An toàn đa luồng (UI Thread Safety):** Khi muốn tương tác với các control giao diện từ luồng phụ, sử dụng `Dispatcher.Invoke(() => { ... })`.
- **Regex & Trích xuất dữ liệu:**
  - Viết Regex tối ưu và sử dụng các tùy chọn `RegexOptions.IgnoreCase` hoặc `RegexOptions.Singleline` phù hợp.
  - Lu�n lu�n gi?i m� HTML Entity c?a ti�u d? truy?n b?ng `WebUtility.HtmlDecode()`.

- **Quy t?c ph�n t�ch file (MainWindow partial classes):** Tuy?t d?i KH�NG vi?t code m?i hay b? sung logic tr?c ti?p v�o file [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). File n�y ch? gi? l?i d?nh nghia khung l?p, c�c tru?ng d? li?u (fields) v� h�m kh?i t?o (constructors). M?i logic/t�nh nang m?i ho?c thay d?i ph?i du?c d?t trong c�c file partial tuong ?ng theo quy chu?n d?t t�n:
  - Logic hệ thống chung (Save, Load, Copy, Clear...): `MainWindow.System*.cs` (ví dụ: [MainWindow.SystemActions.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemActions.cs))
  - Logic theo Tab: `MainWindow.Tab**.cs` (ví dụ: [MainWindow.TabHentaiforce.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.TabHentaiforce.cs))
  - Logic giao diện (Responsive, DataGrid events, context menu...): `MainWindow.UI***.cs` (ví dụ: [MainWindow.UIResponsive.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResponsive.cs), [MainWindow.UIResultsGrid.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResultsGrid.cs))

- **Quy t?c Build:** Lu�n lu�n c?u h�nh, bi�n d?ch v� ch?y d? �n ? ch? d? **Release** d? t?i uu h�a hi?u nang v� d?m b?o t�nh tuong th�ch c?a HandyControl DLL. Kh�ng build ho?c ph�n ph?i b?n Debug. Khi build m� ph�t sinh b?t k? **Warning** hay **Error** n�o, Agent B?T BU?C ph?i t? d?ng t�m hi?u, t? s?a code v� bi�n d?ch l?i li�n t?c cho t?i khi s?ch ho�n to�n l?i v� c?nh b�o (0 warnings, 0 errors) m?i b�n giao.

---

## 3. Quy Tắc Thiết Giao Diện (WPF / XAML)
- **Theme Cyberpunk:** Sử dụng các tài nguyên màu sắc và hiệu ứng được định nghĩa trong [App.xaml](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/App.xaml):
  - **Màu nền:** `CyberpunkDarkBrush` (Màu tối chủ đạo `#0a0d14`)
  - **Màu khung/Panel:** `CyberpunkCardBrush` (`#121622`)
  - **Màu nhấn sáng (Cyan):** `CyberpunkCyanBrush` (`#00f0ff`)
  - **Màu nhấn phụ (Pink):** `CyberpunkPinkBrush` (`#ff007f`)
  - **Màu cảnh báo (Yellow):** `CyberpunkYellowBrush` (`#ffe600`)
  - **Hiệu ứng phát sáng (Neon Glow):** `CyberpunkCyanGlow` và `CyberpunkPinkGlow`.
- **Nút bấm (Button Styles):**
  - Nút màu xanh Cyan: `Style="{StaticResource CyberpunkButtonCyan}"`
  - Nút màu hồng Pink: `Style="{StaticResource CyberpunkButtonPink}"`
- **Khung bao quanh (Border/Cards):** Sử dụng `Style="{StaticResource CyberpunkCard}"` cho các Border bao quanh nhóm chức năng.
- **Tiêu đề chữ:** Sử dụng `Style="{StaticResource CyberpunkHeader}"` cho các tiêu đề văn bản.
- **Quy tắc đặt tên Control:**
  - TextBox: Tiền tố `txt` (Ví dụ: `txtTagUrl`, `txtResultLinks`)
  - Button: Tiền tố `btn` (Ví dụ: `btnScrape`, `btnFetchInfo`)
  - TextBlock / Label: Tiền tố `lbl` (Ví dụ: `lblStatus`, `lblLinkCount`)
  - ProgressBar: `progressBar`

---

## 4. Quy Chuẩn Đọc/Ghi File Markdown (.md)
- **Xuất File:** Khi lưu kết quả, ghi dưới dạng bảng Markdown chuẩn:
  ```markdown
  | No. | Gallery Name | Gallery Link |
  | :--- | :--- | :--- |
  | 1 | Tên truyện | Link |
  ```
- **Xử lý ký tự đặc biệt:** Thay thế ký tự ống `|` trong tiêu đề truyện thành `\|` trước khi lưu vào bảng để tránh hỏng cấu trúc bảng Markdown. Khi load file lên, đổi ngược lại thành `|`.
- **�?c File:** B? qua c�c d�ng ti�u d? v� d�ng ph�n c�ch b?ng m?t c�ch an to�n. Ch? parse nh?ng d�ng h?p l? c� ch?a link b?t d?u b?ng `http`.

---

## 5. Lệnh Build & Chạy Dự Án
- **Khôi phục thư viện:** `nuget restore`
- **Build dự án:** `msbuild "get link manga.csproj" /p:Configuration=Release` hoặc dùng Visual Studio.
- **Đường dẫn file thực thi:** `bin\Release\get link manga.exe`

## 6. Quy tắc git commit
- **Sau khi build file exe ra release thành công, luôn luôn thực hiện git commit & git push lên repo github.**
- **M?i khi t?o commit m?i, b?t bu?c ph?i c?p nh?t l?ch s? (M� commit, Th?i gian, N?i dung) v�o file [log git commit.md](file:///r:/HDD R/ZC SYMLINK/USERS/source/repos/ghostminhtoan/get link manga/log git commit.md) v?i quy t?c: Commit m?i nh?t lu�n n?m ? tr�n c�ng (du?i ph?n ti�u d?).**

---

## 7. Hướng Dẫn Tự Động Hóa Tạo Tab Cào Truyện Mới (Cho AI Agent)
Khi người dùng yêu cầu thêm một trang web cào truyện mới với các thông tin đầu vào sau:
- **Link domain:** Tên miền của trang web cần cào (ví dụ: `https://example.com`)
- **Link book:** Định dạng link của trang chi tiết truyện/danh sách chương (ví dụ: `https://example.com/manga/ten-truyen`)
- **Link chapter:** Định dạng link của trang đọc chương/ảnh (ví dụ: `https://example.com/chapter/123`)
- **Image redirect:** `yes` hoặc `no` (có cần chuyển hướng/cào tiếp trang con để lấy link ảnh trực tiếp hay không)
- **T? d?ng d� s? page:** Thu?t to�n ho?c quy t?c Regex/HTML selector d? t? d?ng t�m ki?m v� tr�ch xu?t t?ng s? trang/chuong t? trang chi ti?t (v� d?: t�m s? trang l?n nh?t trong thanh ph�n trang pagination ho?c t? d�ng ch? tr?ng th�i `Page 1 of X`).

**AI Agent cần tự động thực hiện các bước sau:**

### Bước 1: Thiết kế giao diện (UI) trong [MainWindow.xaml](file:///r:/HDD R/ZC SYMLINK/USERS/source/repos/ghostminhtoan/get link manga/MainWindow.xaml)
1. Xác định trang web thuộc nhóm **MANGA** hay **HENTAI** để thêm thẻ `<TabItem>` vào đúng `TabControl` (`tabManga` hoặc `tabHentai`).
2. Sử dụng `Style="{StaticResource CyberpunkTabItem}"` và chọn một màu sáng độc nhất cho `<TabItem.Tag>` (ví dụ: `#00ff66` cho Green, `#ffe600` cho Yellow, `#00ffff` cho Cyan...).
3. Sao chép cấu trúc giao diện điều khiển cấu hình (Parameters Config) từ các tab có sẵn, đổi tên toàn bộ các Control (TextBox, Button, ToggleButton...) theo đúng tiền tố và tên miền mới (ví dụ: `txtExampleTagUrl`, `btnExampleFetchInfo`, `txtExampleLog`...).

### Bước 2: Tạo File Code Logic Mới `MainWindow.Tab[SiteName].cs`
Tạo một file partial class mới (ví dụ: `MainWindow.TabExample.cs`) để chứa toàn bộ logic xử lý cho trang web đó.
1. **H�m Ph�n T�ch (Analyze Target Page):** 
   - Dùng `_httpClient` tải trang chi tiết (`link book`).
   - Sử dụng Regex hoặc HTML parser để lấy tổng số trang/chương, tiêu đề truyện.
   - Cập nhật thông tin lên giao diện thông qua `Dispatcher.Invoke()`.
2. **Hàm Cào (Start Crawling / Get Link):**
   - Hỗ trợ dừng cào bằng `CancellationToken` (`_cts.Token`).
   - Lặp qua các chương cần tải từ `txtExamplePageFrom` đến `txtExamplePageTo`.
   - Nếu **Image redirect = yes**: Cần cào tiếp trang con (`link chapter`) để tìm thẻ ảnh chứa link ảnh thực tế.
   - Nếu **Image redirect = no**: Lấy trực tiếp link ảnh hiển thị trên trang hiện tại.
   - Add danh sách link cào được vào DataGrid thông qua `GalleryItem`.

### Bước 3: Đăng ký Sự Kiện trong Code-behind
1. Gán các hàm sự kiện Click của Button (`BtnExampleFetchInfo_Click`, `BtnExampleScrape_Click`...) tương ứng vào file code logic.
2. Đảm bảo xử lý lỗi bằng khối `try-catch` và ghi log chi tiết ra ô cấu hình log tương ứng (`txtExampleLog`).

### Bước 4: Kiểm thử & Biên dịch (Build)
1. Chạy lệnh build để đảm bảo dự án biên dịch thành công mà không có lỗi hay cảnh báo.
2. Commit và push thay đổi lên Git theo đúng quy trình.

