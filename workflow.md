# Hướng Dẫn Phát Triển & Quy Tắc Code - Dự Án "get link manga"

Tài liệu này chứa toàn bộ quy tắc code, cấu trúc dự án, hướng dẫn thiết kế giao diện và quy trình làm việc. **Tất cả các Agent AI (như Antigravity, Claude, Cursor, Copilot...) BẮT BUỘC phải đọc file này trước khi thực hiện bất kỳ chỉnh sửa nào.**

---

## 1. Tổng Quan Dự Án
- **Quy tắc trả lời:** Luôn luôn trả lời bằng tiếng Việt. Câu trả lời phải thường xuyên chèn emotion, emotion không được đơn điệu mà phải đa dạng, phong phú và sáng tạo.
- **Quy tắc mã hóa:** Luôn luôn dùng tiếng Việt có dấu, UTF-8.
- **Tên dự án:** get link manga
- **Loại ứng dụng:** Windows Desktop Application (WPF)
- **Framework:** .NET Framework 4.8 (C#)
- **Thư viện UI:** HandyControl (v3.5.0)
- **Chủ đề giao diện (Theme):** Cyberpunk (Tông màu tối, hiệu ứng phát sáng Neon)
- **Tính năng chính:** Cào liên kết truyện tranh từ trang HentaiForce, hiển thị danh sách kết quả, sao chép kết quả vào clipboard (chỉ link hoặc dạng bảng), lưu/đọc kết quả từ file Markdown.

---

## 2. Quy Tắc Code & Lập Trình C#
- **Không tạo mới `HttpClient` mỗi nơi:** Bắt buộc sử dụng instance static `_httpClient` được định nghĩa sẵn trong [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). Nó được cấu hình tự động giải nén (GZip, Deflate) và User-Agent phù hợp.
- **Xử lý bất đồng bộ (Async/Await):** Luôn dùng `async/await` cho các tác vụ I/O (cào web, đọc ghi file) để tránh đơ giao diện.
- **Cơ chế dừng cào (Cancellation):** Các vòng lặp cào dữ liệu phải hỗ trợ `CancellationToken` từ `CancellationTokenSource` (`_cts`). Kiểm tra `token.IsCancellationRequested` để dừng tác vụ kịp thời và đưa nút bấm về trạng thái ban đầu trong khối `finally`.
- **An toàn đa luồng (UI Thread Safety):** Khi muốn tương tác với các control giao diện từ luồng phụ, sử dụng `Dispatcher.Invoke(() => { ... })`.
- **Regex & Trích xuất dữ liệu:**
  - Viết Regex tối ưu và sử dụng các tùy chọn `RegexOptions.IgnoreCase` hoặc `RegexOptions.Singleline` phù hợp.
  - **Luôn luôn giải mã HTML Entity của tiêu đề truyện bằng `WebUtility.HtmlDecode()`**.

- **Quy tắc phân tách file (MainWindow partial classes):** Tuyệt đối KHÔNG viết code mới hay bổ sung logic trực tiếp vào file [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). File này chỉ giữ lại định nghĩa khung lớp, các trường dữ liệu (fields) và hàm khởi tạo (constructors). Mỗi logic/tính năng mới hoặc thay đổi phải được đặt trong các file partial tương ứng theo quy chuẩn đặt tên:
  - Logic hệ thống chung (Save, Load, Copy, Clear...): `MainWindow.System*.cs` (ví dụ: [MainWindow.SystemActions.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemActions.cs))
  - Logic theo Tab: `MainWindow.Tab**.cs` (ví dụ: [MainWindow.TabHentaiforce.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.TabHentaiforce.cs))
  - Logic giao diện (Responsive, DataGrid events, context menu...): `MainWindow.UI***.cs` (ví dụ: [MainWindow.UIResponsive.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResponsive.cs), [MainWindow.UIResultsGrid.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResultsGrid.cs))

- **Quy tắc Build:** Luôn luôn cấu hình, biên dịch và chạy dự án ở chế độ **Release** để tối ưu hóa hiệu năng và đảm bảo tính tương thích của HandyControl DLL. Không build hoặc phân phối bản Debug. Khi build mà phát sinh bất kỳ **Warning** hay **Error** nào, Agent BẮT BUỘC phải tự động tìm hiểu, tự sửa code và biên dịch lại liên tục cho tới khi sạch hoàn toàn lỗi và cảnh báo (0 warnings, 0 errors) mới bàn giao.

---

## 3. Quy Tắc Thiết Kế Giao Diện (WPF / XAML)
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
- **Đọc File:** Bỏ qua các dòng tiêu đề và dòng phân cách bằng một cách an toàn. Chỉ parse những dòng hợp lệ có chứa link bắt đầu bằng `http`.

---

## 5. Lệnh Build & Chạy Dự Án
- **Khôi phục thư viện:** `nuget restore`
- **Build dự án:** Luôn luôn dùng đúng lệnh:
  ```powershell
  "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\Admin\source\repos\ghostminhtoan\get link manga\Comic-GMTPC.csproj" /p:Configuration=Release /p:Platform=AnyCPU
  ```
- **Đường dẫn file thực thi:** `bin\Release\get link manga.exe`
- **Build time:** Khi code xong và build chốt thành công thì ghi build time đúng thời điểm đó, không lấy lại theo các lần rebuild sau trong Visual Studio. Format bắt buộc: `2026-06-07 12.44.28 PM Sunday` (năm-tháng-ngày ∕ giờ.phút.giây AM/PM Thứ, trong đó `∕` là ký tự unicode riêng, không phải `/` trên bàn phím).

## 6. Quy tắc git commit
- **Sau khi build file exe ra release thành công, luôn luôn thực hiện git commit & git push lên repo github.**
- **Mỗi khi tạo commit mới, bắt buộc phải cập nhật lịch sử (Mã commit, Build Time, Nội dung) vào file [log git commit.md](file:///r:/HDD R/ZC SYMLINK/USERS/source/repos/ghostminhtoan/get link manga/log git commit.md) với quy tắc: Commit mới nhất luôn nằm ở trên cùng (dưới phần tiêu đề). Build Time phải là thời điểm build chốt, không phải thời điểm Visual Studio rebuild sau đó.**

---

## 7. Hướng Dẫn Tự Động Hóa Tạo Tab Cào Truyện Mới (Cho AI Agent)
Khi người dùng yêu cầu thêm một trang web cào truyện mới với các thông tin đầu vào sau:
- **Link domain:** Tên miền của trang web cần cào (ví dụ: `https://example.com`)
- **Link book:** Định dạng link của trang chi tiết truyện/danh sách chương (ví dụ: `https://example.com/manga/ten-truyen`)
- **Link chapter:** Định dạng link của trang đọc chương/ảnh (ví dụ: `https://example.com/chapter/123`)
- **Image redirect:** `yes` hoặc `no` (có cần chuyển hướng/cào tiếp trang con để lấy link ảnh trực tiếp hay không)
- **Tự động đếm số page:** Thuật toán hoặc quy tắc Regex/HTML selector để tự động tìm kiếm và trích xuất tổng số trang/chương từ trang chi tiết (ví dụ: tìm số trang lớn nhất trong thành phần trang pagination hoặc tự động check trạng thái `Page 1 of X`).

**AI Agent cần tự động thực hiện các bước sau:**

### Bước 1: Thiết kế giao diện (UI) trong [MainWindow.xaml](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml)
1. Xác định trang web thuộc nhóm **MANGA** hay **HENTAI** để thêm thẻ `<TabItem>` vào đúng `TabControl` (`tabManga` hoặc `tabHentai`).
2. Sử dụng `Style="{StaticResource CyberpunkTabItem}"` và chọn một màu sáng độc nhất cho `<TabItem.Tag>` (ví dụ: `#00ff66` cho Green, `#ffe600` cho Yellow, `#00ffff` cho Cyan...).
3. Sao chép cấu trúc giao diện điều khiển cấu hình (Parameters Config) từ các tab có sẵn, đổi tên toàn bộ các Control (TextBox, Button, ToggleButton...) theo đúng tiền tố và tên miền mới (ví dụ: `txtExampleTagUrl`, `btnExampleFetchInfo`, `txtExampleLog`...).

### Bước 2: Tạo File Code Logic Mới `MainWindow.Tab[SiteName].cs`
Tạo một file partial class mới (ví dụ: `MainWindow.TabExample.cs`) để chứa toàn bộ logic xử lý cho trang web đó.
1. **Hàm Phân Tích (Analyze Target Page):**
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
