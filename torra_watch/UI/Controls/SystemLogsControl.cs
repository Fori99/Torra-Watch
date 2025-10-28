using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace torra_watch.UI.Controls
{
    public enum LogLevel { Debug, Info, Success, Warning, Error }

    public sealed class LogEntryVM
    {
        public DateTime TimeUtc { get; init; } = DateTime.UtcNow;
        public LogLevel Level { get; init; } = LogLevel.Info;
        public string Message { get; init; } = "";
    }

    public partial class SystemLogsControl : UserControl
    {

        // Theme tokens
        private static readonly Color HeaderBg = Color.FromArgb(248, 249, 251);
        private static readonly Color HeaderText = Color.FromArgb(60, 66, 72);
        private static readonly Color GridLines = Color.FromArgb(234, 236, 239);
        private static readonly Color RowText = Color.FromArgb(33, 37, 41);

        private static readonly Color DebugText = Color.FromArgb(120, 125, 130);
        private static readonly Color InfoText = RowText;
        private static readonly Color SuccessText = Color.FromArgb(21, 87, 36);
        private static readonly Color WarnText = Color.FromArgb(133, 100, 4);
        private static readonly Color ErrorText = Color.FromArgb(114, 28, 36);

        private readonly List<LogEntryVM> _items = new();
        private readonly ListViewEx _lv = new();
        private readonly ContextMenuStrip _menu = new();
        private bool _autoScroll = true;
        private int _maxLines = 5000;

        public SystemLogsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(8);

            // ListView setup (virtual + owner draw for colors)
            _lv.Dock = DockStyle.Fill;
            _lv.BorderStyle = BorderStyle.None;
            _lv.FullRowSelect = true;
            _lv.HideSelection = false;
            _lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _lv.View = View.Details;
            _lv.GridLines = false;
            _lv.OwnerDraw = true;
            _lv.VirtualMode = true;
            _lv.RetrieveVirtualItem += Lv_RetrieveVirtualItem;
            _lv.DrawColumnHeader += Lv_DrawColumnHeader;
            _lv.DrawSubItem += Lv_DrawSubItem;
            _lv.VirtualListSize = 0;
            _lv.Scrolled += (_, __) => _autoScroll = IsAtBottom();


            _lv.Columns.Add("Time", 110);
            _lv.Columns.Add("Level", 72);
            _lv.Columns.Add("Message", 600);

            _lv.Resize += (_, __) => AutoSizeColumns();

            // context menu
            var miCopy = new ToolStripMenuItem("Copy selected", null, (_, __) => CopySelected());
            var miClear = new ToolStripMenuItem("Clear", null, (_, __) => Clear());
            var miAuto = new ToolStripMenuItem("Auto-scroll") { Checked = _autoScroll };
            miAuto.Click += (_, __) => { _autoScroll = !miAuto.Checked; miAuto.Checked = _autoScroll; };
            _menu.Items.AddRange(new ToolStripItem[] { miCopy, miClear, new ToolStripSeparator(), miAuto });
            _lv.ContextMenuStrip = _menu;



            Controls.Add(_lv);
            ApplyHeaderStyle();
        }

        // ---------- Public API ----------
        public int MaxLines
        {
            get => _maxLines;
            set { _maxLines = Math.Max(100, value); TrimIfNeeded(); Redraw(); }
        }

        public void Append(LogEntryVM item)
        {
            _items.Add(item);
            TrimIfNeeded();
            _lv.VirtualListSize = _items.Count;
            if (_autoScroll) ScrollToBottom();
        }

        public void AppendRange(IEnumerable<LogEntryVM> items)
        {
            _items.AddRange(items);
            TrimIfNeeded();
            _lv.VirtualListSize = _items.Count;
            if (_autoScroll) ScrollToBottom();
        }

        public void Clear()
        {
            _items.Clear();
            _lv.VirtualListSize = 0;
            _lv.Invalidate();
        }

        // ---------- Internals ----------
        private void Lv_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            var it = _items[e.ItemIndex];
            e.Item = new ListViewItem(new[]
            {
                it.TimeUtc.ToLocalTime().ToString("HH:mm:ss"),
                it.Level.ToString(),
                it.Message
            });
        }

        private void Lv_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var bg = new SolidBrush(HeaderBg);
            using var pen = new Pen(GridLines);
            using var textBrush = new SolidBrush(HeaderText);
            e.Graphics.FillRectangle(bg, e.Bounds);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Segoe UI Semibold", 9f),
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 8, e.Bounds.Width - 16, e.Bounds.Height - 16),
                HeaderText, TextFormatFlags.EndEllipsis);
        }

        private void Lv_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            var entry = _items[e.ItemIndex];
            var color = entry.Level switch
            {
                LogLevel.Debug => DebugText,
                LogLevel.Info => InfoText,
                LogLevel.Success => SuccessText,
                LogLevel.Warning => WarnText,
                LogLevel.Error => ErrorText,
                _ => RowText
            };

            // row background
            using var bg = new SolidBrush(Color.White);
            e.Graphics.FillRectangle(bg, e.Bounds);

            // text
            var text = e.SubItem.Text;
            var rect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, e.Bounds.Width - 12, e.Bounds.Height - 8);
            var font = e.ColumnIndex == 2
                ? new Font("Segoe UI", 9f)
                : new Font("Segoe UI", 9f);
            TextRenderer.DrawText(e.Graphics, text, font, rect, color, TextFormatFlags.EndEllipsis);
        }

        private void ApplyHeaderStyle()
        {
            _lv.BackColor = Color.White;
            _lv.ForeColor = RowText;
        }

        private void AutoSizeColumns()
        {
            if (_lv.Columns.Count < 3) return;
            var w = _lv.ClientSize.Width;
            _lv.Columns[0].Width = 110;     // Time
            _lv.Columns[1].Width = 80;      // Level
            _lv.Columns[2].Width = Math.Max(100, w - _lv.Columns[0].Width - _lv.Columns[1].Width - 8);
        }

        private void TrimIfNeeded()
        {
            var over = _items.Count - _maxLines;
            if (over > 0) _items.RemoveRange(0, over);
        }

        private void ScrollToBottom()
        {
            if (_items.Count == 0) return;
            _lv.EnsureVisible(_items.Count - 1);
        }

        // add near the class:
        const int LVM_GETCOUNTPERPAGE = 0x1000 + 40;
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private bool IsAtBottom()
        {
            if (_items.Count == 0 || _lv.TopItem == null) return true;

            int top = _lv.TopItem.Index;
            int perPage = SendMessage(_lv.Handle, LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero).ToInt32();
            if (perPage <= 0) perPage = Math.Max(1, _lv.ClientSize.Height / Math.Max(1, _lv.Font.Height + 10));

            return top + perPage >= _items.Count - 1;
        }

        private void CopySelected()
        {
            if (_lv.SelectedIndices.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            foreach (int i in _lv.SelectedIndices)
            {
                var it = _items[i];
                sb.AppendLine($"{it.TimeUtc:HH:mm:ss} {it.Level,-7} {it.Message}");
            }
            Clipboard.SetText(sb.ToString());
        }

        private void Redraw() => _lv.Invalidate();


    }

    sealed class ListViewEx : ListView
    {
        public event EventHandler? Scrolled;

        // Win32 messages
        const int WM_VSCROLL = 0x0115;
        const int WM_MOUSEWHEEL = 0x020A;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL)
                Scrolled?.Invoke(this, EventArgs.Empty);
        }
    }
}
