using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace torra_watch.UI.Controls
{
    /// <summary>
    /// Card container with optional header, actions area, rounded/square border,
    /// and a Body panel for content.
    /// </summary>
    public partial class CardFrameControl : UserControl
    {
        // Header theme
        private static readonly Color HeaderBackColor = Color.FromArgb(248, 249, 251);
        private static readonly Color HeaderTextColor = Color.FromArgb(60, 66, 72);

        // Children
        private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 38, BackColor = HeaderBackColor };
        private readonly Label _title = new() { AutoSize = false, Dock = DockStyle.Fill, Padding = new Padding(12, 9, 8, 0) };
        private readonly Panel _actions = new() { Dock = DockStyle.Right, Width = 1, BackColor = HeaderBackColor };
        private readonly Panel _body = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };

        // State
        private bool _headerVisible = true;
        private Color _borderColor = Color.FromArgb(239, 240, 242); // your rgba
        private int _borderThickness = 1;                           // device pixels
        private int _cornerRadius = 8;                              // 0 = square

        public CardFrameControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            BackColor = Color.White;
            Margin = new Padding(6);

            // Header
            _title.Text = "Title";
            _title.Font = new Font("Segoe UI Semibold", 10f);
            _title.ForeColor = HeaderTextColor;
            _header.Controls.Add(_title);
            _header.Controls.Add(_actions);
            _header.Paint += Header_Paint;

            // Compose
            Controls.Add(_body);
            Controls.Add(_header);

            // Make space so children don't cover the border
            Padding = new Padding(_borderThickness);
        }

        // ============== Public API ==============

        [Browsable(true), Category("Appearance")]
        public string Title
        {
            get => _title.Text;
            set { _title.Text = value; Invalidate(_header.Bounds); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Panel Body => _body;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Panel Actions => _actions;

        [Browsable(true), DefaultValue(true), Category("Appearance")]
        public bool HeaderVisible
        {
            get => _headerVisible;
            set
            {
                if (_headerVisible == value) return;
                _headerVisible = value;
                _header.Visible = value;
                Invalidate();
                PerformLayout();
            }
        }

        /// <summary>Border color (default rgba(239,240,242,1)).</summary>
        [Browsable(true), Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        /// <summary>Border thickness in device pixels. Minimum 1.</summary>
        [Browsable(true), Category("Appearance")]
        public int BorderThickness
        {
            get => _borderThickness;
            set
            {
                var v = Math.Max(1, value);
                if (_borderThickness == v) return;
                _borderThickness = v;
                Padding = new Padding(_borderThickness); // keep the edge free
                Invalidate();
                PerformLayout();
            }
        }

        /// <summary>Corner radius in pixels. Set 0 for square corners.</summary>
        [Browsable(true), Category("Appearance")]
        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                var v = Math.Max(0, value);
                if (_cornerRadius == v) return;
                _cornerRadius = v;
                UpdateRegionForCorners();
                Invalidate();
            }
        }

        // ============== Paint ==============

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = _cornerRadius > 0 ? SmoothingMode.AntiAlias : SmoothingMode.Default;

            // Inset by half the pen width for crisp lines
            float half = _borderThickness / 2f;
            var rect = new RectangleF(
                half,
                half,
                Math.Max(0, Width - _borderThickness),
                Math.Max(0, Height - _borderThickness)
            );

            using var bg = new SolidBrush(BackColor);
            using var pen = new Pen(_borderColor, _borderThickness);

            if (_cornerRadius > 0)
            {
                using var path = CreateRoundedPath(rect, _cornerRadius);
                g.FillPath(bg, path);
                g.DrawPath(pen, path);
            }
            else
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        private void Header_Paint(object? sender, PaintEventArgs e)
        {
            if (!_headerVisible) return;

            // 1px divider scaled to DPI
            var dpi = DeviceDpi <= 0 ? 96 : DeviceDpi;
            var px = Math.Max(1, (int)Math.Round(dpi / 96f));
            var y = _header.Height - px;

            using var pen = new Pen(_borderColor, px);
            e.Graphics.DrawLine(pen, 0, y, _header.Width, y);
        }

        // ============== Layout / Region ==============

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegionForCorners();
            Invalidate();
        }

        private void UpdateRegionForCorners()
        {
            if (_cornerRadius <= 0) { Region = null; return; }
            var r = ClientRectangle;
            if (r.Width <= 1 || r.Height <= 1) { Region = null; return; }
            r.Width -= 1;
            r.Height -= 1;
            using var gp = CreateRoundedPath(r, _cornerRadius);
            Region = new Region(gp);
        }

        // ============== Helpers ==============

        private static GraphicsPath CreateRoundedPath(RectangleF bounds, int radius)
        {
            float d = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2f);
            var p = new GraphicsPath();
            p.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            p.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            p.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            p.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
            => CreateRoundedPath(RectangleF.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), radius);
    }
}
