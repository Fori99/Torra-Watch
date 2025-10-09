using System.Drawing;
using System.Windows.Forms;

namespace torra_watch.UI.Controler
{
    public sealed class LogPanel : UserControl
    {
        public ListBox List { get; } = new() { Dock = DockStyle.Fill };

        public LogPanel()
        {
            Dock = DockStyle.Fill;
            var card = new CardPanel { Dock = DockStyle.Fill };
            List.BackColor = UiTheme.Bg3;
            List.ForeColor = UiTheme.Text;
            List.BorderStyle = BorderStyle.None;
            List.Font = UiTheme.Mono;
            card.Controls.Add(List);
            Controls.Add(card);
        }

        public void Append(string msg) => List.Items.Add(msg);
    }
}
