using System;
using System.Drawing;
using System.Windows.Forms;

namespace torra_watch.UI.Controler
{
    public sealed class HeaderBar : UserControl
    {
        // public surface so MainForm can update text
        public Label Equity { get; } = new() { AutoSize = true, Text = "Equity: …" };
        public Label State { get; } = new() { AutoSize = true, Text = "State: Idle" };
        public Label Next { get; } = new() { AutoSize = true, Text = "Next check: —" };
        public Label Env { get; } = new() { AutoSize = true, Text = "Env: —", ForeColor = Color.DimGray };

        public Button BtnStart { get; } = new() { Text = "Start" };
        public Button BtnStop { get; } = new() { Text = "Stop", Enabled = false };
        public Button BtnCheck { get; } = new() { Text = "Check" };
        public Button BtnPanic { get; } = new() { Text = "Panic" };
        public Button BtnAcct { get; } = new() { Text = "Account" };
        public Button BtnOrders { get; } = new() { Text = "Orders" };

        public GroupBox SettingsBox { get; } = new() { Text = "Settings", Dock = DockStyle.Fill };

        public HeaderBar()
        {
            Dock = DockStyle.Top;
            AutoSize = true;
            BackColor = UiTheme.Bg0;

            // left: stacked labels
            var status = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Margin = new(0, 0, 16, 0)
            };
            status.Controls.Add(Equity);
            status.Controls.Add(State);
            status.Controls.Add(Next);
            status.Controls.Add(Env);

            // middle: buttons
            var actions = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new(0, 0, 16, 0)
            };
            actions.Controls.AddRange(new Control[] { BtnStart, BtnStop, BtnCheck, BtnPanic, BtnAcct, BtnOrders });

            // right: settings placeholder (MainForm will add content inside SettingsBox)
            var header = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // settings stretches

            header.Controls.Add(status, 0, 0);
            header.Controls.Add(actions, 1, 0);
            header.Controls.Add(SettingsBox, 2, 0);

            Controls.Add(header);

            // theme
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            Equity.ForeColor = UiTheme.Accent;
            State.ForeColor = UiTheme.Text;
            Next.ForeColor = UiTheme.TextDim;

            StyleButton(BtnStart, UiTheme.Success);
            StyleButton(BtnStop, UiTheme.Danger);
            StyleButton(BtnCheck, UiTheme.Blend(UiTheme.Bg3, UiTheme.Text, .12));
            StyleButton(BtnPanic, UiTheme.Warning);
            StyleButton(BtnAcct, UiTheme.Blend(UiTheme.Bg3, UiTheme.Text, .12));
            StyleButton(BtnOrders, UiTheme.Blend(UiTheme.Bg3, UiTheme.Text, .12));
        }

        private static void StyleButton(Button btn, Color fill)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = fill;
            btn.ForeColor = Color.Black;
            btn.Font = new Font("Segoe UI Semibold", 10f);
            btn.Height = 34;
            btn.Margin = new Padding(6, 0, 0, 0);
            btn.Padding = new Padding(10, 3, 10, 3);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = UiTheme.Blend(fill, Color.Black, 0.3);
        }
    }
}
