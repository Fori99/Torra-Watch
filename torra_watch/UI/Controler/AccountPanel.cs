using System.Drawing;
using System.Windows.Forms;

namespace torra_watch.UI.Controler
{
    // Make sure CardPanel : Panel (or Control). If it doesn't, inherit Panel instead.
    public class AccountPanel : CardPanel
    {
        private readonly Label _title;
        private readonly Label _content;

        public AccountPanel()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(12);

            _title = new Label
            {
                Text = "Account Details",
                AutoSize = true,
                Dock = DockStyle.Top,
                Font = UiTheme.H2,
                ForeColor = UiTheme.Text
            };

            _content = new Label
            {
                Text = "—",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = UiTheme.Body,
                ForeColor = UiTheme.Text
            };

            Controls.Add(_content);
            Controls.Add(_title);
        }

        public void SetText(string text) => _content.Text = text;

        // ✅ Correct signature: matches Control.Dispose(bool)
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _title?.Dispose();
                _content?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
