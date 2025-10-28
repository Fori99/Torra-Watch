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
    public partial class LiveOrdersListControl : UserControl
    {
        private readonly Label _lblHeader = new();
        private readonly Panel _stack = new();

        // optional: light “card body” bg like the screenshot
        private static readonly Color ColCardBody = Color.FromArgb(247, 249, 252);

        public bool ShowHeader { get => _lblHeader.Visible; set => _lblHeader.Visible = value; }
        public string HeaderText { get => _lblHeader.Text; set => _lblHeader.Text = value; }

        public LiveOrdersListControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(8, 6, 8, 6);

            _lblHeader.AutoSize = true;
            _lblHeader.Text = "Live Orders";
            _lblHeader.Font = new Font("Segoe UI Semibold", 9f);
            _lblHeader.ForeColor = Color.FromArgb(116, 124, 133);
            _lblHeader.Margin = new Padding(0, 0, 0, 6);

            // a subtle body panel to mimic the light section background
            var body = new Panel { Dock = DockStyle.Fill, BackColor = ColCardBody, Padding = new Padding(8) };


            _stack.Dock = DockStyle.Fill;
            _stack.AutoScroll = true;
            _stack.Padding = new Padding(0);
            _stack.Margin = new Padding(0);
            body.Controls.Add(_stack);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(_lblHeader, 0, 0);
            root.Controls.Add(body, 0, 1);

            Controls.Add(root);
        }

        public void SetOrders(IEnumerable<OrderVM> orders)
        {
            _stack.SuspendLayout();
            _stack.Controls.Clear();

            var list = (orders ?? Enumerable.Empty<OrderVM>()).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var row = new LiveOrdersItemControl { Dock = DockStyle.Top };
                row.Bind(list[i]);

                // add row
                _stack.Controls.Add(row);
                row.BringToFront();

                // add a spacer after it (except last)
                if (i < list.Count - 1)
                {
                    _stack.Controls.Add(new Panel
                    {
                        Dock = DockStyle.Top,
                        Height = 8,                // ← space between “cards”
                        BackColor = ColCardBody,   // same as body background so the gap reads as spacing
                    });
                }
            }

            _stack.ResumeLayout();
        }

        public void Demo()
        {
            SetOrders(new[]
            {
            new OrderVM { Symbol="BTC", LastPrice=42150m, Buy=41800m, TakeProfit=42500m, StopLoss=41500m },
        });
        }
    }
}
