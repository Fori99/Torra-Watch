using System.Runtime.InteropServices;
using System.Text;
using torra_watch.UI.ViewModels;

namespace torra_watch.UI.Controls
{
    public partial class SystemLogsControl : UserControl
    {
        // ---- Theme tokens
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

        // add near other fields
        private readonly ToolTip _tip = new();
        private int _lastTipIndex = -1;
        private const int MsgCol = 2;
        private readonly Font _rowFont = new("Segoe UI", 9f);


        // cached fonts
        private readonly Font _fontHeader = new("Segoe UI Semibold", 9f);
        private readonly Font _fontCell = new("Segoe UI", 9f);

        public SystemLogsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(8);

            // --- ListView (virtual + owner draw)
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

            _lv.Columns.Add("Time", 110);
            _lv.Columns.Add("Level", 80);
            _lv.Columns.Add("Message", 900);

            _lv.Scrollable = true;
            _lv.Columns[MsgCol].Width = 900;        // big default
            _lv.MouseMove += Lv_MouseMove;
            _lv.MouseLeave += (_, __) => { _tip.Hide(_lv); _lastTipIndex = -1; };
            _lv.DoubleClick += Lv_DoubleClick;


            _lv.Resize += (_, __) => AutoSizeColumns();
            _lv.Scrolled += (_, __) => _autoScroll = IsAtBottom();

            // --- Context menu
            var miCopy = new ToolStripMenuItem("Copy selected", null, CopySelected);
            var miClear = new ToolStripMenuItem("Clear", null, (_, __) => Clear());
            var miAuto = new ToolStripMenuItem("Auto-scroll") { Checked = _autoScroll };
            miAuto.Click += (_, __) =>
            {
                _autoScroll = !_autoScroll;
                miAuto.Checked = _autoScroll;
            };
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

        /// <summary>Convenience overload so callers can log plain text.</summary>


        public void Clear()
        {
            if (InvokeRequired) { BeginInvoke(new Action(Clear)); return; }

            _items.Clear();
            _lv.VirtualListSize = 0;
            _lv.Invalidate();
        }

        private void CopySelected(object? sender, EventArgs e)
        {
            if (_lv.SelectedIndices.Count == 0) return;

            var sb = new StringBuilder();
            foreach (int i in _lv.SelectedIndices)
            {
                var it = _items[i];
                // If your LogEntryVM uses Timestamp (recommended), use it here:
                sb.AppendLine($"{it.Timestamp:HH:mm:ss} {it.Level,-7} {it.Message}");
                // If your VM property is TimeUtc, then:
                // sb.AppendLine($"{it.TimeUtc:HH:mm:ss} {it.Level,-7} {it.Message}");
            }
            Clipboard.SetText(sb.ToString());
        }


        // ---------- ListView virtual/paint ----------
        private void Lv_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            var it = _items[e.ItemIndex];

            // Prefer Timestamp; if you had an older VM with TimeUtc, show that too.
            var when = it.Timestamp != default ? it.Timestamp
                     : (it.GetType().GetProperty("TimeUtc")?.GetValue(it) as DateTime? ?? DateTime.UtcNow);

            e.Item = new ListViewItem(new[]
             {
                it.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                it.Level.ToString(),
                it.Message ?? "(empty)"  // Add null coalescing
            });

        }

        private void Lv_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var bg = new SolidBrush(HeaderBg);
            using var pen = new Pen(GridLines);
            using var textBrush = new SolidBrush(HeaderText);

            e.Graphics.FillRectangle(bg, e.Bounds);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            TextRenderer.DrawText(e.Graphics,
                e.Header.Text,
                _fontHeader,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height),
                HeaderText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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

            using var bg = new SolidBrush(Color.White);
            e.Graphics.FillRectangle(bg, e.Bounds);

            // Use full bounds height for proper vertical alignment
            var rect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);

            var flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
            if (e.ColumnIndex != MsgCol)
                flags |= TextFormatFlags.EndEllipsis;

            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                _rowFont,
                rect,
                color,
                flags
            );
        }



        // ---------- Helpers ----------
        private void ApplyHeaderStyle()
        {
            _lv.BackColor = Color.White;
            _lv.ForeColor = RowText;
        }

        private void AutoSizeColumns()
        {
            if (_lv.Columns.Count < 3) return;
            _lv.Columns[0].Width = 110;   // Time
            _lv.Columns[1].Width = 80;    // Level

            // Only raise minimum; never force a smaller width for the message column.
            var minMsg = Math.Max(100, _lv.ClientSize.Width - _lv.Columns[0].Width - _lv.Columns[1].Width - 8);
            if (_lv.Columns[MsgCol].Width < minMsg)
                _lv.Columns[MsgCol].Width = minMsg;
        }


        private void Lv_MouseMove(object? sender, MouseEventArgs e)
        {
            var hit = _lv.HitTest(e.Location);
            if (hit.Item == null) { _tip.Hide(_lv); _lastTipIndex = -1; return; }

            int index = hit.Item.Index;
            if (index == _lastTipIndex) return;

            _lastTipIndex = index;
            var it = _items[index];
            _tip.Show(it.Message ?? string.Empty, _lv, e.Location + new Size(16, 16), 5000);
        }

        private void Lv_DoubleClick(object? sender, EventArgs e)
        {
            if (_lv.SelectedIndices.Count == 0) return;
            int i = _lv.SelectedIndices[0];
            var it = _items[i];
            using var dlg = new Form
            {
                Text = $"{it.Timestamp:HH:mm:ss}  {it.Level}",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(720, 360)
            };
            var tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 10f),
                Text = it.Message ?? string.Empty
            };
            dlg.Controls.Add(tb);
            dlg.ShowDialog(this);
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

        private void Redraw() => _lv.Invalidate();

        // ---- “At bottom” detection (virtual list)
        private const int LVM_GETCOUNTPERPAGE = 0x1000 + 40;
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private bool IsAtBottom()
        {
            if (_items.Count == 0 || _lv.TopItem == null) return true;

            int top = _lv.TopItem.Index;
            int perPage = SendMessage(_lv.Handle, LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero).ToInt32();
            if (perPage <= 0) perPage = Math.Max(1, _lv.ClientSize.Height / Math.Max(1, _lv.Font.Height + 10));

            return top + perPage >= _items.Count - 1;
        }

        private void EnsureMsgWidth(string msg)
        {
            if (_lv.Columns.Count <= MsgCol) return;
            var sz = TextRenderer.MeasureText(msg ?? string.Empty, _rowFont);
            var need = sz.Width + 40;                     // some padding
            if (need > _lv.Columns[MsgCol].Width)
                _lv.Columns[MsgCol].Width = need;         // enables horizontal scrollbar automatically
        }

        public void Append(LogEntryVM item)
        {
            _items.Add(item);
            TrimIfNeeded();
            _lv.VirtualListSize = _items.Count;
            EnsureMsgWidth(item.Message);
            if (_autoScroll) ScrollToBottom();
        }
        public void Append(string message, LogLevel level = LogLevel.Info)
        {
            Append(new LogEntryVM
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message ?? string.Empty
            });
        }

        public void AppendRange(IEnumerable<LogEntryVM> items)
        {
            foreach (var it in items) EnsureMsgWidth(it.Message);
            _items.AddRange(items);
            TrimIfNeeded();
            _lv.VirtualListSize = _items.Count;
            if (_autoScroll) ScrollToBottom();
        }


    }

    // --- ListView extension with Scroll event
    sealed class ListViewEx : ListView
    {
        public event EventHandler? Scrolled;

        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL)
                Scrolled?.Invoke(this, EventArgs.Empty);
        }
    }
}
