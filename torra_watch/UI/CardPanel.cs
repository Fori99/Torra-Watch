using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using torra_watch.UI;

namespace torra_watch.UI
{
    public class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = UiTheme.Bg2;
            Padding = new Padding(14);
            Margin = new Padding(10);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = ClientRectangle;
            rect.Inflate(-1, -1);

            // shadow
            using (var shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            {
                var sh = rect; sh.Offset(0, 3);
                using var gpSh = Rounded(sh, UiTheme.Radius + 1);
                g.FillPath(shadow, gpSh);
            }

            // fill
            using var gp = Rounded(rect, UiTheme.Radius);
            using var fill = new SolidBrush(BackColor);
            using var pen = new Pen(UiTheme.Stroke);
            g.FillPath(fill, gp);
            g.DrawPath(pen, gp);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var gp = new GraphicsPath();
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }
    }
}
