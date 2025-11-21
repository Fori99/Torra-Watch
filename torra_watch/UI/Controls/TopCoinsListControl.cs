using torra_watch.UI.ViewModels;

namespace torra_watch.UI.Controls
{
    public partial class TopCoinsListControl : UserControl
    {
        private readonly Label _lblTitle = new();
        private readonly FlowLayoutPanel _stack = new();

        public TopCoinsListControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(8);

            // Header
            _lblTitle.AutoSize = true;
            _lblTitle.Font = new Font("Segoe UI", 9f);
            _lblTitle.ForeColor = Color.FromArgb(100, 107, 114);
            _lblTitle.Text = "Top Coins — Δ 3h";
            _lblTitle.Margin = new Padding(4, 0, 4, 6);

            // Vertical stack for row cards
            _stack.Dock = DockStyle.Fill;
            _stack.FlowDirection = FlowDirection.TopDown;
            _stack.WrapContents = false;
            _stack.AutoScroll = true;
            _stack.Padding = new Padding(2, 0, 2, 6); // small horizontal inset so cards don't kiss the border
            _stack.BackColor = Color.White;
            _stack.SizeChanged += (_, __) => AdjustRowWidths();

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            //root.Controls.Add(_lblTitle, 0, 0);
            root.Controls.Add(_stack, 0, 0);

            Controls.Add(root);
        }

        private int _hours = 3;
        public void SetWindowHours(int hours)
        {
            _hours = hours;
            _lblTitle.Text = $"Top Coins — Δ {hours}h";
        }

        public void SetCoins(IEnumerable<TopCoinVM> coins, int maxRows = 150)
        {
            var data = coins?.Take(maxRows).ToList() ?? new List<TopCoinVM>();
            _stack.SuspendLayout();
            _stack.Controls.Clear();

            foreach (var c in data)
                _stack.Controls.Add(BuildRowCard(c));

            _stack.ResumeLayout();
            AdjustRowWidths();                                     // <— add this
        }

        private string Money(decimal value)
        {
            // Smart formatting based on price magnitude
            if (value < 0.01m)
                return $"${value:N6}";      // $0.000123
            else if (value < 1m)
                return $"${value:N4}";      // $0.1234
            else if (value < 100m)
                return $"${value:N2}";      // $31.26
            else if (value < 10000m)
                return $"${value:N2}";      // $1,234.56
            else
                return $"${value:N0}";      // $42,150
        }



        // ---------- UI helpers ----------
        private Control BuildRowCard(TopCoinVM c)
        {
            var card = new RowCard { Height = 64, Margin = new Padding(2, 2, 2, 8) };

            // ===== Left icon (blue square with $ if no icon) =====
            var iconBox = new PictureBox
            {
                Size = new Size(28, 28),
                Location = new Point(12, 18),          // vertically centered-ish
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.FromArgb(219, 234, 254) // light blue
            };
            if (c.Icon != null) { iconBox.Image = c.Icon; iconBox.BackColor = Color.Transparent; }
            else
            {
                // tiny $ glyph
                var bmp = new Bitmap(16, 16);
                using var g = Graphics.FromImage(bmp);
                g.DrawString("$", new Font("Segoe UI", 9f, FontStyle.Bold),
                    new SolidBrush(Color.FromArgb(37, 99, 235)), new PointF(0, -1));
                iconBox.Image = bmp;
            }

            // ===== Text block (symbol + current price, prev price below) =====
            var textPanel = new Panel { Left = 52, Top = 12, Width = card.Width - 160, Height = 40, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };

            var lblSymbol = new Label
            {
                AutoSize = true,
                Text = c.Symbol,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new Point(0, 0)
            };

            var lblPrice = new Label
            {
                AutoSize = true,
                Text = Money(c.Price),
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(73, 80, 87),
                Location = new Point(0, 20)
            };

            // compute prev price if not supplied: Price = Prev * (1 + pct/100)
            var prev = c.PrevPrice ?? (c.ChangePct == -100 ? 0 : c.Price / (1 + (c.ChangePct / 100m)));
            var lblPrev = new Label
            {
                AutoSize = true,
                Text = $"was {Money(prev)} {_hours}h ago",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(134, 142, 150),
                Location = new Point(lblPrice.Right + 8, 22)
            };

            textPanel.Controls.Add(lblSymbol);
            textPanel.Controls.Add(lblPrice);
            textPanel.Controls.Add(lblPrev);

            // ===== Change % on the right =====
            var up = c.ChangePct >= 0;
            var lblPct = new Label
            {
                Width = 100,
                Height = 24,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = up ? Color.FromArgb(25, 135, 84) : Color.FromArgb(220, 53, 69),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(card.Width - 100 - 12, 20),
                Text = (up ? "▲ " : "▼ ") + c.ChangePct.ToString("0.##") + "%"
            };
            card.Resize += (_, __) => lblPct.Left = card.Width - 100 - 12; // keep right-aligned

            card.Controls.Add(iconBox);
            card.Controls.Add(textPanel);
            card.Controls.Add(lblPct);
            return card;
        }



        private void AdjustRowWidths()
        {
            var w = Math.Max(100, _stack.ClientSize.Width - 4);    // small inset
            foreach (Control child in _stack.Controls)
                child.Width = w;
        }


        // small rounded “card” with 1px border (matches Figma tiles)
        private sealed class RowCard : Panel
        {
            private static readonly Color Border = Color.FromArgb(222, 226, 230);  // #DEE2E6
            private static readonly Color Back = Color.White;

            public RowCard()
            {
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                Padding = new Padding(0);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);

                using var bg = new SolidBrush(Back);
                using var pen = new Pen(Border);

                g.FillRectangle(bg, rect);       // flat fill
                g.DrawRectangle(pen, rect);      // crisp 1px border, 90° corners
            }

            private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
            {
                int d = radius * 2;
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
