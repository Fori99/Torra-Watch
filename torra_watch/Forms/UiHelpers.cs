using System.Drawing;
using System.Windows.Forms;
using torra_watch.UI;

internal static class UiHelpers
{
    private static void MakePillInPlace(Button btn, string text, Color fill, Color? border = null)
    {
        btn.Text = text;
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = fill;
        btn.ForeColor = Color.Black;
        btn.Font = new Font("Segoe UI Semibold", 10f);
        btn.Height = 34;
        btn.Margin = new Padding(6, 0, 0, 0);
        btn.Padding = new Padding(10, 3, 10, 3);

        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = border ?? UiTheme.Blend(fill, Color.Black, 0.3);
        btn.FlatAppearance.MouseOverBackColor = UiTheme.Blend(fill, Color.White, 0.05);
        btn.FlatAppearance.MouseDownBackColor = UiTheme.Blend(fill, Color.Black, 0.05);

        // make the button a pill using Region; keep updating on resize
        void setRegion(object? s, EventArgs e)
        {
            int r = btn.Height / 2;
            var gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddArc(0, 0, r, r, 90, 180);
            gp.AddArc(btn.Width - r, 0, r, r, 270, 180);
            gp.CloseAllFigures();
            btn.Region = new Region(gp);
        }
        btn.Resize -= setRegion; // avoid multiple handlers if restyled
        btn.Resize += setRegion;
        setRegion(btn, EventArgs.Empty);
    }

}
