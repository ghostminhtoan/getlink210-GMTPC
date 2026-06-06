using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private enum LogFilterMode
        {
            All,
            Error
        }

        private sealed class LogEntry
        {
            public string Text { get; set; }
            public bool IsError { get; set; }
        }

        private sealed class LogPanelState
        {
            public string Key { get; set; }
            public RichTextBox Box { get; set; }
            public ToggleButton AutoScrollToggle { get; set; }
            public LogFilterMode Mode { get; set; } = LogFilterMode.All;
            public List<LogEntry> Entries { get; } = new List<LogEntry>();
        }

        private readonly Dictionary<string, LogPanelState> _logPanels = new Dictionary<string, LogPanelState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<RichTextBox, string> _logPanelKeys = new Dictionary<RichTextBox, string>();

        private void InitializeLogPanels()
        {
            RegisterLogPanel("main", txtLog, chkAutoScrollLog);
            RegisterLogPanel("nhentai", txtNhentaiLog, chkAutoScrollNhentaiLog);
            RegisterLogPanel("vihentai", txtViHentaiLog, chkAutoScrollViHentaiLog);
            RegisterLogPanel("truyenqq", txtTruyenqqLog, chkAutoScrollTruyenqqLog);
            RegisterLogPanel("nettruyen", txtNettruyenLog, chkAutoScrollNettruyenLog);
            RegisterLogPanel("hentaiera", txtHentaieraLog, chkAutoScrollHentaieraLog);
        }

        private void RegisterLogPanel(string key, RichTextBox box, ToggleButton autoScrollToggle)
        {
            if (box == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var state = new LogPanelState
            {
                Key = key,
                Box = box,
                AutoScrollToggle = autoScrollToggle
            };

            _logPanels[key] = state;
            _logPanelKeys[box] = key;
        }

        private LogPanelState GetLogPanelState(RichTextBox box)
        {
            if (box == null)
            {
                return null;
            }

            if (_logPanelKeys.TryGetValue(box, out string key) && _logPanels.TryGetValue(key, out LogPanelState state))
            {
                return state;
            }

            return null;
        }

        private void AppendLogLineToDocument(RichTextBox rtb, string text, bool isError)
        {
            if (rtb == null)
            {
                return;
            }

            var brush = isError ? System.Windows.Media.Brushes.Red : null;

            var paragraph = rtb.Document.Blocks.LastBlock as Paragraph;
            if (paragraph == null)
            {
                paragraph = new Paragraph { Margin = new Thickness(0) };
                rtb.Document.Blocks.Add(paragraph);
            }

            var run = new Run(text);
            if (brush != null)
            {
                run.Foreground = brush;
            }
            paragraph.Inlines.Add(run);
        }

        private void RenderLogPanel(LogPanelState state)
        {
            if (state == null || state.Box == null)
            {
                return;
            }

            state.Box.Document.Blocks.Clear();
            foreach (var entry in state.Entries)
            {
                if (state.Mode == LogFilterMode.Error && !entry.IsError)
                {
                    continue;
                }

                AppendLogLineToDocument(state.Box, entry.Text, entry.IsError);
            }

            if (state.AutoScrollToggle?.IsChecked == true)
            {
                ScrollTextBoxToEnd(state.Box);
            }
        }

        internal void AppendLogLineWithFilter(RichTextBox rtb, string text, bool isError)
        {
            if (rtb == null)
            {
                return;
            }

            var state = GetLogPanelState(rtb);
            if (state == null)
            {
                AppendLogLineToDocument(rtb, text, isError);
                return;
            }

            state.Entries.Add(new LogEntry { Text = text, IsError = isError });
            if (state.Mode == LogFilterMode.Error && !isError)
            {
                return;
            }

            AppendLogLineToDocument(rtb, text, isError);
            if (state.AutoScrollToggle?.IsChecked == true)
            {
                ScrollTextBoxToEnd(rtb);
            }
        }

        internal void ClearLogPanel(RichTextBox rtb)
        {
            var state = GetLogPanelState(rtb);
            if (state != null)
            {
                state.Entries.Clear();
            }

            if (rtb != null)
            {
                rtb.Document.Blocks.Clear();
            }
        }

        private void SetLogFilter(string key, LogFilterMode mode)
        {
            if (string.IsNullOrWhiteSpace(key) || !_logPanels.TryGetValue(key, out LogPanelState state))
            {
                return;
            }

            state.Mode = mode;
            RenderLogPanel(state);
        }

        private LogFilterMode GetLogFilter(string key)
        {
            if (!string.IsNullOrWhiteSpace(key) && _logPanels.TryGetValue(key, out LogPanelState state))
            {
                return state.Mode;
            }

            return LogFilterMode.All;
        }

        private void SyncLogFilterMenu(ContextMenu menu, string key)
        {
            if (menu == null)
            {
                return;
            }

            var mode = GetLogFilter(key);
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                if (string.Equals(item.Tag as string, "All", StringComparison.OrdinalIgnoreCase))
                {
                    item.IsChecked = mode == LogFilterMode.All;
                }
                else if (string.Equals(item.Tag as string, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    item.IsChecked = mode == LogFilterMode.Error;
                }
            }
        }

        private void BtnLogFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                string key = button.Tag as string;
                SyncLogFilterMenu(button.ContextMenu, key);
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void LogFilterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem menuItem))
            {
                return;
            }

            if (!(menuItem.Parent is ContextMenu menu) || !(menu.PlacementTarget is Button button))
            {
                return;
            }

            string key = button.Tag as string;
            string selectedMode = menuItem.Tag as string;
            SetLogFilter(key, string.Equals(selectedMode, "Error", StringComparison.OrdinalIgnoreCase) ? LogFilterMode.Error : LogFilterMode.All);
            SyncLogFilterMenu(menu, key);
        }
    }
}
