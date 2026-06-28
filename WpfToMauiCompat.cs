// Global type aliases must be at the very top of the file
global using SQLiteConnection = Microsoft.Data.Sqlite.SqliteConnection;
global using SQLiteCommand = Microsoft.Data.Sqlite.SqliteCommand;
global using SQLiteTransaction = Microsoft.Data.Sqlite.SqliteTransaction;

global using Button = Microsoft.Maui.Controls.Button;
global using Border = Microsoft.Maui.Controls.Border;
global using Grid = Microsoft.Maui.Controls.Grid;
global using Label = Microsoft.Maui.Controls.Label;
global using Brush = Microsoft.Maui.Controls.Brush;

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Maui.ApplicationModel;

namespace System.Windows
{
    public class RoutedEventArgs : EventArgs { public bool Handled { get; set; } }
    public class RoutedEventArgs<T> : RoutedEventArgs { }
    public class DependencyObject { }
    public class DependencyProperty { }
    public class UIElement : FrameworkElement { }
    
    public class FrameworkElement : DependencyObject 
    {
        public double ActualWidth { get; set; } = 800;
        public double ActualHeight { get; set; } = 600;
        public object DataContext { get; set; }
        public object Tag { get; set; }
    }
    
    public class Window : FrameworkElement
    {
        public void Close() { }
        public void Show() { }
        public bool? ShowDialog() => true;
        public double Width { get; set; }
        public double Height { get; set; }
        public string Title { get; set; }
        public System.Windows.Threading.Dispatcher Dispatcher => System.Windows.Threading.Dispatcher.CurrentDispatcher;
    }

    public static class MessageBox
    {
        public static void Show(string msg, string title = "", MessageBoxButton btn = MessageBoxButton.OK, MessageBoxImage img = MessageBoxImage.None)
        {
            Console.WriteLine($"[MessageBox] {title}: {msg}");
        }
    }

    public enum MessageBoxButton { OK, OKCancel, YesNo, YesNoCancel }
    public enum MessageBoxImage { Information, Warning, Error, Question, None }
    public enum MessageBoxResult { OK, Yes, No, Cancel, None }
    
    public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);
}

namespace System.Windows.Threading
{
    public class Dispatcher
    {
        public static Dispatcher CurrentDispatcher { get; } = new Dispatcher();
        public void BeginInvoke(Delegate method, params object[] args)
        {
            MainThread.BeginInvokeOnMainThread(() => method.DynamicInvoke(args));
        }
        public void Invoke(Action action)
        {
            MainThread.InvokeOnMainThreadAsync(action).Wait();
        }
    }
    public enum DispatcherPriority
    {
        Send, Normal, Background, Idle
    }
    
    public class DispatcherTimer
    {
        public TimeSpan Interval { get; set; }
        public event EventHandler Tick;
        public void Start() { }
        public void Stop() { }
    }
}

namespace System.Windows.Controls
{
    public class UIElementCompat : System.Windows.FrameworkElement
    {
        public bool IsEnabled { get; set; } = true;
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class Control : UIElementCompat { }
    public class ContentControl : Control { public object Content { get; set; } }
    
    public class TextBox : Control
    {
        public string Text { get; set; } = string.Empty;
        public void ScrollToEnd() { }
        public void AppendText(string text) { Text += text; }
    }
    
    public class TextBlock : FrameworkElement
    {
        public string Text { get; set; } = string.Empty;
    }
    
    public class ComboBox : Control
    {
        public int SelectedIndex { get; set; } = 0;
        public object SelectedItem { get; set; }
        public IList Items { get; set; } = new List<object>();
        public string Text { get; set; } = string.Empty;
    }
    
    public class Button : Control
    {
        public event EventHandler Click;
    }
    
    public class DataGrid : Control
    {
        public IEnumerable ItemsSource { get; set; }
        public object SelectedItem { get; set; }
        public IList SelectedItems { get; set; } = new List<object>();
    }
    
    public class StackPanel : Panel { }
    public class Panel : Control { public IList Children { get; set; } = new List<object>(); }
    public class Border : Control { }
    public class RichTextBox : Control 
    {
        public void ScrollToEnd() { }
    }
    public class UserControl : Control { }
    public class ListBox : Control 
    { 
        public IList SelectedItems { get; set; } = new List<object>(); 
        public object SelectedItem { get; set; } 
    }
    public class ContextMenu : Control { public bool IsOpen { get; set; } }
    public class ProgressBar : Control { public double Value { get; set; } }
}

namespace System.Windows.Input
{
    public class KeyEventArgs : EventArgs { public Key Key { get; set; } }
    public enum Key { D1, Enter, Escape, Space }
    public class MouseButtonEventArgs : EventArgs { }
    public class MouseWheelEventArgs : EventArgs { public int Delta { get; set; } }
    public class MouseEventArgs : EventArgs { }
    public class RoutedUICommand { }
    public class ExecutedRoutedEventArgs : EventArgs { }
}

namespace System.Windows.Forms
{
    public class FolderBrowserDialog
    {
        public string SelectedPath { get; set; } = string.Empty;
        public DialogResult ShowDialog() => DialogResult.OK;
    }
    public enum DialogResult { OK, Cancel }
    
    public class NotifyIcon
    {
        public bool Visible { get; set; }
        public string ToolTipText { get; set; } = string.Empty;
        public void Dispose() { }
    }
}

namespace System.Windows.Data
{
    public class CollectionViewSource { }
}

namespace System.Windows.Media
{
    public class Brush { }
    public class Brushes
    {
        public static Brush Red { get; } = new Brush();
        public static Brush Green { get; } = new Brush();
        public static Brush Black { get; } = new Brush();
        public static Brush Yellow { get; } = new Brush();
        public static Brush Cyan { get; } = new Brush();
    }
}

namespace Microsoft.Maui.Dispatching
{
    public static class DispatcherExtensions
    {
        public static void BeginInvoke(this IDispatcher dispatcher, Action action)
        {
            dispatcher.Dispatch(action);
        }
        public static void Invoke(this IDispatcher dispatcher, Action action)
        {
            dispatcher.Dispatch(action);
        }
        public static Task InvokeAsync(this IDispatcher dispatcher, Action action)
        {
            dispatcher.Dispatch(action);
            return Task.CompletedTask;
        }
        public static Task<T> InvokeAsync<T>(this IDispatcher dispatcher, Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            dispatcher.Dispatch(() => {
                try { tcs.SetResult(func()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }
        public static Task InvokeAsync(this IDispatcher dispatcher, Func<Task> func)
        {
            var tcs = new TaskCompletionSource();
            dispatcher.Dispatch(async () => {
                try { await func(); tcs.SetResult(); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }
    }
}

namespace System.Windows.Interop
{
    public class WindowInteropHelper
    {
        public WindowInteropHelper(object window) { }
        public IntPtr Handle { get; } = IntPtr.Zero;
    }
    
    public class HwndSource
    {
        public static HwndSource FromHwnd(IntPtr hwnd) => new HwndSource();
        public void AddHook(object hook) { }
        public void RemoveHook(object hook) { }
    }
}

namespace System.Data.SQLite
{
    public class SQLiteConnection : Microsoft.Data.Sqlite.SqliteConnection 
    {
        public SQLiteConnection(string connectionString) : base(connectionString) { }
    }
    
    public class SQLiteCommand : Microsoft.Data.Sqlite.SqliteCommand 
    {
        public SQLiteCommand(string commandText, Microsoft.Data.Sqlite.SqliteConnection connection) : base(commandText, connection) { }
        public SQLiteCommand(string commandText, Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction) : base(commandText, connection, transaction) { }
    }
}

// Stubs for excluded windows & classes
public class CaptchaWindow 
{ 
    public string ResolvedCookie { get; set; } = string.Empty; 
    public string ResolvedUserAgent { get; set; } = string.Empty; 
    public void Show() { } 
}
public class BookmarkHistoryWindow { }
public class DuplicateWindow { }
public class HakoChapterCaptureResult 
{ 
    public bool Success { get; set; } 
    public string Html { get; set; } = string.Empty; 
}
public class SystemFloatingControlWindow { }
