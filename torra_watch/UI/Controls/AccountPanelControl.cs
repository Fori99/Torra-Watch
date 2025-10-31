using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using torra_watch.Models;
using torra_watch.UI.ViewModels;

namespace torra_watch.UI.Controls
{
    public partial class AccountPanelControl : UserControl
    {
        private readonly Label _lblHeader = new();   // “Main Balance”
        private readonly Label _lblUsdt = new();   // big USDT value
        private readonly Label _lblTotal = new();   // “Total = … USDT” (optional)
        private readonly DataGridView _grid = new();

        private readonly BindingSource _bs = new();          // AccountVM
        private readonly BindingSource _bsHoldings = new();  // List<HoldingVM>

        // Optional backing fields if you want to reuse data later
        private decimal _totalUsdt;
        private List<Balance> _balances = new();
        private List<Position> _positions = new();

        // TODO: point these to your real UI elements
        // If you already have labels/grids with other names, change them here.
        private Label? _lblTotalUsdt;        // e.g., the big "85,230.00 USDT" label
        private DataGridView? _gridBalances; // table for balances
        private DataGridView? _gridOrders;   // table for open orders/positions

        /// <summary>Update the big total balance number.</summary>
        public void SetTotalUsdt(decimal totalUsdt)
        {
            _totalUsdt = totalUsdt;
            // if you have a label in the designer named lblTotalUsdt, assign it to _lblTotalUsdt in ctor.
            if (_lblTotalUsdt != null)
                _lblTotalUsdt.Text = $"{totalUsdt:N2} USDT";
            // otherwise do nothing; you can wire it later.
        }

        /// <summary>Replace the balances table.</summary>
        public void SetBalances(IEnumerable<Balance> balances)
        {
            _balances = balances?.ToList() ?? new List<Balance>();
            if (_gridBalances != null)
            {
                // Simple bind; you can style the grid elsewhere
                _gridBalances.AutoGenerateColumns = true;
                _gridBalances.DataSource = _balances
                    .Select(b => new
                    {
                        Asset = b.Asset,
                        Qty = b.Qty,
                        EstUSDT = Math.Round(b.EstUsdt, 2)
                    })
                    .ToList();
            }
        }

        /// <summary>Replace the open orders/positions table.</summary>
        public void SetOpenOrders(IEnumerable<Position> positions)
        {
            _positions = positions?.ToList() ?? new List<Position>();
            if (_gridOrders != null)
            {
                _gridOrders.AutoGenerateColumns = true;
                _gridOrders.DataSource = _positions
                    .Select(p => new
                    {
                        Symbol = p.Symbol,
                        Entry = p.Price,
                        TP = p.TakeProfit,
                        SL = p.StopLoss
                    })
                    .ToList();
            }
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

            // Make it display-only
            _grid.ReadOnly = true;
            _grid.TabStop = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // Kill selection colors (match normal colors)
            _grid.DefaultCellStyle.SelectionBackColor = Color.White;
            _grid.DefaultCellStyle.SelectionForeColor = _grid.DefaultCellStyle.ForeColor;
            _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = _grid.AlternatingRowsDefaultCellStyle.BackColor;
            _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = _grid.DefaultCellStyle.ForeColor;

            // Never show a selected row
            _grid.SelectionChanged += (_, __) => _grid.ClearSelection();

            // Apply theme
            ApplyGridTheme(_grid);

            // Columns (Asset · Qty · Est. USDT)
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

            // Compose
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
            _grid.CurrentCell = null; // ensure no focus cell
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
            // Colors (match your card header/borders)
            var headerBg = Color.FromArgb(248, 249, 251); // #F8F9FB
            var headerText = Color.FromArgb(60, 66, 72);    // #3C4248
            var gridLines = Color.FromArgb(234, 236, 239); // soft separators
            var altRowBg = Color.FromArgb(249, 250, 252); // very light gray
            var selBg = Color.FromArgb(233, 245, 238); // subtle greenish (fits Start/Success)
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
