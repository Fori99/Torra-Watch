using System.Data;
using torra_watch.Models;
using torra_watch.UI.ViewModels;

namespace torra_watch.UI.Controls
{
    public partial class AccountPanelControl : UserControl
    {
        private readonly Label _lblHeader = new();
        private readonly Label _lblUsdt = new();
        private readonly Label _lblTotal = new();
        private readonly DataGridView _grid = new();

        private readonly BindingSource _bs = new();
        private readonly BindingSource _bsHoldings = new();

        private decimal _totalUsdt;
        private List<Balance> _balances = new();
        private List<Position> _positions = new();

        /// <summary>Update the big total balance number.</summary>
        public void SetTotalUsdt(decimal totalUsdt)
        {
            _totalUsdt = totalUsdt;

            // Update the actual labels that exist!
            _lblUsdt.Text = $"{totalUsdt:N2} USDT";
            _lblTotal.Text = $"Total: {totalUsdt:N2} USDT";

            // Force refresh
            _lblUsdt.Invalidate();
            _lblTotal.Invalidate();
        }

        /// <summary>Replace the balances table.</summary>
        /// <summary>Replace the balances table.</summary>
        public void SetBalances(IEnumerable<Balance> balances)
        {
            _balances = balances?.ToList() ?? new List<Balance>();

            // Convert Balance to HoldingVM format that the grid expects
            var holdings = _balances.Select(b => new HoldingVM
            {
                Asset = b.Asset,
                Free = b.Qty,
                Locked = 0m,
                // Total is calculated automatically (Free + Locked)
                EstUsdt = Math.Round(b.EstUsdt, 2),
                PercentOfTotal = _totalUsdt > 0 ? Math.Round(b.EstUsdt / _totalUsdt * 100m, 2) : 0
            }).ToList();

            _bsHoldings.DataSource = holdings;
            _bsHoldings.ResetBindings(false);
            _grid.ClearSelection();
            _grid.CurrentCell = null;
            _grid.Refresh();
        }

        /// <summary>Replace the open orders/positions table.</summary>
        public void SetOpenOrders(IEnumerable<Position> positions)
        {
            _positions = positions?.ToList() ?? new List<Position>();
            // Note: You don't have a grid for positions in this control yet
            // If you want to show positions, you'll need to add another grid
        }

        public AccountPanelControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(8);

            // Top area (labels)
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                ColumnCount = 2,
                Padding = new Padding(4, 0, 4, 6)
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _lblHeader.Text = "Main Balance (USDT)";
            _lblHeader.AutoSize = true;
            _lblHeader.Font = new Font("Segoe UI", 9f);
            _lblHeader.ForeColor = Color.FromArgb(100, 107, 114);
            _lblHeader.Margin = new Padding(2, 6, 2, 0);

            _lblUsdt.Text = "0.00 USDT";
            _lblUsdt.AutoSize = true;
            _lblUsdt.Font = new Font("Segoe UI Semibold", 20f);
            _lblUsdt.ForeColor = Color.FromArgb(33, 37, 41);
            _lblUsdt.Margin = new Padding(2, 2, 2, 0);

            _lblTotal.Text = "Total: 0.00 USDT";
            _lblTotal.AutoSize = true;
            _lblTotal.Font = new Font("Segoe UI", 9f);
            _lblTotal.ForeColor = Color.FromArgb(100, 107, 114);
            _lblTotal.TextAlign = ContentAlignment.TopRight;
            _lblTotal.Dock = DockStyle.Fill;
            _lblTotal.Margin = new Padding(2, 12, 2, 0);

            var leftStack = new Panel { Dock = DockStyle.Fill };
            leftStack.Controls.Add(_lblUsdt);
            leftStack.Controls.Add(_lblHeader);
            _lblUsdt.Location = new Point(_lblHeader.Left, _lblHeader.Bottom + 2);

            top.Controls.Add(leftStack, 0, 0);
            top.Controls.Add(_lblTotal, 1, 0);

            // Grid
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ColumnHeadersHeight = 34;
            _grid.RowTemplate.Height = 28;

            _grid.ReadOnly = true;
            _grid.TabStop = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            _grid.DefaultCellStyle.SelectionBackColor = Color.White;
            _grid.DefaultCellStyle.SelectionForeColor = _grid.DefaultCellStyle.ForeColor;
            _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = _grid.AlternatingRowsDefaultCellStyle.BackColor;
            _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = _grid.DefaultCellStyle.ForeColor;

            _grid.SelectionChanged += (_, __) => _grid.ClearSelection();

            ApplyGridTheme(_grid);

            // Columns
            _grid.Columns.Clear();
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Asset",
                DataPropertyName = "Asset",
                FillWeight = 34
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Qty",
                DataPropertyName = "Total",
                FillWeight = 33,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N8"
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Est. USDT",
                DataPropertyName = "EstUsdt",
                FillWeight = 33,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N2"
                }
            });

            _grid.DataSource = _bsHoldings;

            Controls.Add(_grid);
            Controls.Add(top);
        }

        /// <summary>Bind account data to the panel.</summary>
        public void SetData(AccountVM vm)
        {
            _bsHoldings.DataSource = vm.OtherHoldings;
            _lblUsdt.Text = $"{vm.MainUsdt:N2} USDT";
            _lblTotal.Text = $"Total: {vm.TotalUsdt:N2} USDT";

            _grid.ClearSelection();
            _grid.CurrentCell = null;
        }

        /// <summary> Optional: show some fake data while wiring things. </summary>
        public void Demo()
        {
            var demo = new AccountVM
            {
                MainUsdt = 85230.00m,
                OtherHoldings = new List<HoldingVM> {
                    new() { Asset = "BTC", Free = 0.12000000m, Locked = 0m, EstUsdt = 5100m },
                    new() { Asset = "ETH", Free = 2.50000000m,  Locked = 0m, EstUsdt = 4600m },
                    new() { Asset = "SOL", Free = 95.00000000m, Locked = 0m, EstUsdt = 900m },
                }
            };
            var total = demo.TotalUsdt;
            foreach (var h in demo.OtherHoldings) h.PercentOfTotal = total > 0 ? Math.Round(h.EstUsdt / total * 100m, 2) : 0;
            SetData(demo);
        }

        private static void ApplyGridTheme(DataGridView g)
        {
            var headerBg = Color.FromArgb(248, 249, 251);
            var headerText = Color.FromArgb(60, 66, 72);
            var gridLines = Color.FromArgb(234, 236, 239);
            var altRowBg = Color.FromArgb(249, 250, 252);
            var selBg = Color.FromArgb(233, 245, 238);
            var selText = Color.FromArgb(21, 87, 36);

            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = gridLines;

            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = headerBg,
                ForeColor = headerText,
                Font = new Font("Segoe UI Semibold", 9f),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };

            g.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.FromArgb(33, 37, 41),
                Font = new Font("Segoe UI", 9f),
                SelectionBackColor = selBg,
                SelectionForeColor = selText
            };

            g.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = altRowBg
            };

            g.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        }
    }
}