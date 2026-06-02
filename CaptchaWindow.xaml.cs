using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Navigation;

namespace get_link_manga
{
    public partial class CaptchaWindow : Window
    {
        public CookieContainer ResolvedCookies { get; private set; } = new CookieContainer();
        public string UserAgent { get; private set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private readonly string _targetUrl;

        // Suppress script errors via COM interop
        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object QueryService(ref Guid guidService, ref Guid riid);
        }

        public CaptchaWindow(string targetUrl)
        {
            InitializeComponent();
            _targetUrl = targetUrl;
            Loaded += CaptchaWindow_Loaded;
        }

        private void CaptchaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                webView.Navigate(new Uri(_targetUrl));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi tạo trình duyệt: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private void WebView_LoadCompleted(object sender, NavigationEventArgs e)
        {
            // Page finished loading
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get cookies from WinInet cookie store (shared with WPF WebBrowser)
                var uri = new Uri("https://nhentai.net");
                ResolvedCookies = new CookieContainer();

                string cookieHeader = GetCookiesFromUri(uri);
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    ResolvedCookies.SetCookies(uri, cookieHeader);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi thu thập cookies: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetGetCookieEx(string url, string cookieName,
            System.Text.StringBuilder cookieData, ref int size, int dwFlags, IntPtr lpReserved);

        private static string GetCookiesFromUri(Uri uri)
        {
            const int INTERNET_COOKIE_HTTPONLY = 0x2000;
            int datasize = 256;
            var sb = new System.Text.StringBuilder(datasize);
            if (InternetGetCookieEx(uri.ToString(), null, sb, ref datasize, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
                return sb.ToString();
            if (datasize > 0)
            {
                sb = new System.Text.StringBuilder(datasize);
                if (InternetGetCookieEx(uri.ToString(), null, sb, ref datasize, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
                    return sb.ToString();
            }
            return string.Empty;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
