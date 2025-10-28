using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace torra_watch.UI.Controls
{
    public partial class ControlPanelControl : UserControl
    {
        // Events you can subscribe to in MainForm
        public event EventHandler? StartRequested;
        public event EventHandler? StopRequested;
        public event EventHandler? PanicRequested;
        public event EventHandler? OrdersRequested;
        public event EventHandler? AccountRequested;
        public event EventHandler? CheckRequested;

        private readonly Button _btnStart = new();
        private readonly Button _btnStop = new();
        private readonly Button _btnPanic = new();
        private readonly Button _btnOrders = new();
        private readonly Button _btnAccount = new();
        private readonly Button _btnCheck = new();

        public ControlPanelControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(8);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4
            };

            for (int c = 0; c < 3; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));

            // Top row
            InitButton(_btnStart, "Start", Color.FromArgb(39, 168, 68), OnStart);
            InitButton(_btnStop, "Stop", Color.FromArgb(219, 53, 70), OnStop);
            InitButton(_btnPanic, "Panic", Color.FromArgb(254, 193, 7), OnPanic);

            // Bottom row
            InitButton(_btnOrders, "Orders", Neutral, OnOrders);
            InitButton(_btnAccount, "Account", Neutral, OnAccount);
            InitButton(_btnCheck, "Check", Neutral, OnCheck);

            grid.Controls.Add(_btnStart, 0, 0);
            grid.Controls.Add(_btnStop, 1, 0);
            grid.Controls.Add(_btnPanic, 2, 0);

            grid.Controls.Add(_btnOrders, 0, 2);
            grid.Controls.Add(_btnAccount, 1, 2);
            grid.Controls.Add(_btnCheck, 2, 2);

            Controls.Add(grid);

            // initial state
            SetRunning(false);
        }

        // Public helper to toggle states from outside
        public void SetRunning(bool isRunning)
        {
            _btnStart.Enabled = !isRunning;
            _btnStop.Enabled = isRunning;
            _btnCheck.Enabled = true;
            _btnPanic.Enabled = true; // keep enabled always
        }

        // ---- Styling helpers
        private static readonly Color Accent = Color.FromArgb(32, 134, 54);   // green
        private static readonly Color Danger = Color.FromArgb(220, 53, 69);   // red
        private static readonly Color Muted = Color.FromArgb(108, 117, 125); // gray
        private static readonly Color Neutral = Color.FromArgb(52, 58, 64);    // dark

        private static void InitButton(Button b, string text, Color color, EventHandler onClick)
        {
            b.Text = text;
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(6);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = color;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 10f);
            b.Height = 42;
            b.Cursor = Cursors.Hand;
            b.Click += onClick;

            // simple hover effect
            b.MouseEnter += (_, __) => b.BackColor = ControlPaint.Light(color, 0.15f);
            b.MouseLeave += (_, __) => b.BackColor = color;
        }

        // ---- Event raisers
        private void OnStart(object? s, EventArgs e) => StartRequested?.Invoke(this, EventArgs.Empty);
        private void OnStop(object? s, EventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
        private void OnPanic(object? s, EventArgs e) => PanicRequested?.Invoke(this, EventArgs.Empty);
        private void OnOrders(object? s, EventArgs e) => OrdersRequested?.Invoke(this, EventArgs.Empty);
        private void OnAccount(object? s, EventArgs e) => AccountRequested?.Invoke(this, EventArgs.Empty);
        private void OnCheck(object? s, EventArgs e) => CheckRequested?.Invoke(this, EventArgs.Empty);
    }
}

