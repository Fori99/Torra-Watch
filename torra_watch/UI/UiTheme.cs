using System.Drawing;

namespace torra_watch.UI
{
    internal static class UiTheme
    {
        // Base
        public static readonly Color Bg0 = ColorTranslator.FromHtml("#0E1116"); // window
        public static readonly Color Bg1 = ColorTranslator.FromHtml("#12151B"); // panels
        public static readonly Color Bg2 = ColorTranslator.FromHtml("#181C24"); // cards
        public static readonly Color Bg3 = ColorTranslator.FromHtml("#1E2430"); // inputs
        public static readonly Color Stroke = ColorTranslator.FromHtml("#2A3240");

        // Text
        public static readonly Color Text = ColorTranslator.FromHtml("#D7DEE9");
        public static readonly Color TextDim = ColorTranslator.FromHtml("#9AA7B2");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6E7A86");

        // Accent
        public static readonly Color Accent = ColorTranslator.FromHtml("#E3B341"); // gold
        public static readonly Color AccentSoft = Color.FromArgb(24, Accent);
        public static readonly Color Success = ColorTranslator.FromHtml("#24C08B");
        public static readonly Color Danger = ColorTranslator.FromHtml("#F34E4E");
        public static readonly Color Warning = ColorTranslator.FromHtml("#FF9F43");

        // Sizes
        public const int Radius = 12;
        public static readonly Font H1 = new Font("Segoe UI Semibold", 16f);
        public static readonly Font H2 = new Font("Segoe UI Semibold", 13f);
        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font Mono = new Font("Consolas", 10f);

        public static Color Blend(Color a, Color b, double t)
        {
            int Lerp(int c1, int c2) => c1 + (int)((c2 - c1) * t);
            return Color.FromArgb(
                Lerp(a.A, b.A), Lerp(a.R, b.R), Lerp(a.G, b.G), Lerp(a.B, b.B));
        }
    }
}
