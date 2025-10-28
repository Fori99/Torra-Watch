using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using torra_watch.UI.ViewModels;

namespace torra_watch.UI.Controls
{
    public partial class LiveOrdersItemControl : UserControl
    {
        private readonly Label _lblSymbol = new();
        private readonly Label _lblLast = new();

        private readonly Label _lblBuyCap = new();
        private readonly Label _lblTpCap = new();
        private readonly Label _lblSlCap = new();

        private readonly Label _lblBuy = new();
        private readonly Label _lblTp = new();
        private readonly Label _lblSl = new();

        // palette (tuned to the screenshot)
        private static readonly Color ColCaption = Color.FromArgb(116, 124, 133);
        private static readonly Color ColText = Color.FromArgb(55, 59, 64);
        private static readonly Color ColGreen = Color.FromArgb(16, 158, 85);
        private static readonly Color ColRed = Color.FromArgb(205, 49, 49);
        private static readonly Color ColDivider = Color.FromArgb(220, 224, 228);
        private static readonly Color ColOutline = Color.FromArgb(220, 224, 228); // light gray border
        public bool IsLast { get; set; } // list will set this on the final row

        public LiveOrdersItemControl()
        {
            const int RowH = 20;
            const int HeaderH = 24;

            Padding = new Padding(12, 10, 12, 10);
            var totalH = HeaderH + (3 * RowH) + Padding.Vertical;
            Height = totalH;  // ≈ 104

            Dock = DockStyle.Top;
            Margin = new Padding(0);                    // rows touch; divider draws the separation
            Padding = new Padding(12, 10, 12, 10);
            BackColor = Color.Transparent;                // card provides bg
            BorderStyle = BorderStyle.None;

            // draw a thin rectangle border
            this.Paint += (_, e) =>
            {
                using var pen = new Pen(ColOutline, 1);
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                e.Graphics.DrawRectangle(pen, rect);
            };
            this.Resize += (_, __) => Invalidate();

            // header (symbol on left, last price on right)
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 24,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _lblSymbol.AutoSize = true;
            _lblSymbol.Font = new Font("Segoe UI Semibold", 9.5f);
            _lblSymbol.ForeColor = ColText;

            _lblLast.AutoSize = true;
            _lblLast.Font = new Font("Segoe UI Semibold", 9.5f);
            _lblLast.ForeColor = ColText;
            _lblLast.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            header.Controls.Add(_lblSymbol, 0, 0);
            header.Controls.Add(_lblLast, 1, 0);

            // grid (Buy / TP / SL)
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // captions
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // spacer
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // values

            for (int r = 0; r < 3; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

            ConfigureCap(_lblBuyCap, "Buy:");
            ConfigureCap(_lblTpCap, "TP:");
            ConfigureCap(_lblSlCap, "SL:");
            ConfigureValue(_lblBuy, ColText);
            ConfigureValue(_lblTp, ColGreen);
            ConfigureValue(_lblSl, ColRed);

            grid.Controls.Add(_lblBuyCap, 0, 0);
            grid.Controls.Add(_lblTpCap, 0, 1);
            grid.Controls.Add(_lblSlCap, 0, 2);

            grid.Controls.Add(new Label() { AutoSize = true }, 1, 0); // spacer cells
            grid.Controls.Add(new Label() { AutoSize = true }, 1, 1);
            grid.Controls.Add(new Label() { AutoSize = true }, 1, 2);

            grid.Controls.Add(_lblBuy, 2, 0);
            grid.Controls.Add(_lblTp, 2, 1);
            grid.Controls.Add(_lblSl, 2, 2);

            Controls.Add(grid);
            Controls.Add(header);

            // thin bottom divider (like the mock)
            Paint += (s, e) =>
            {
                if (IsLast) return;
                using var pen = new Pen(ColDivider, 1);
                var y = Height - 1;
                e.Graphics.DrawLine(pen, 0, y, Width, y);
            };
            Resize += (_, __) => Invalidate();
        }

        private static void ConfigureCap(Label lbl, string text)
        {
            lbl.AutoSize = true;
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 9f);
            lbl.ForeColor = ColCaption;
            lbl.Margin = new Padding(0, 0, 8, 0);
            lbl.Padding = new Padding(0);
            lbl.TextAlign = ContentAlignment.MiddleLeft;
        }
        private static void ConfigureValue(Label lbl, Color color)
        {
            lbl.AutoSize = true;
            lbl.Font = new Font("Segoe UI Semibold", 9f);
            lbl.ForeColor = color;
            lbl.Margin = new Padding(0);
            lbl.Padding = new Padding(0);
            lbl.TextAlign = ContentAlignment.MiddleRight;
            lbl.Anchor = AnchorStyles.Right;
        }

        public void Bind(OrderVM vm)
        {
            string Cur(decimal v) => $"{vm.QuoteSymbol}{v:#,0.##}";
            _lblSymbol.Text = vm.Symbol.ToUpperInvariant();
            _lblLast.Text = Cur(vm.LastPrice);

            _lblBuy.Text = Cur(vm.Buy);
            _lblTp.Text = Cur(vm.TakeProfit);
            _lblSl.Text = Cur(vm.StopLoss);
        }
    }
}
