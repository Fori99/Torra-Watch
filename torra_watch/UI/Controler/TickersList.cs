using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using torra_watch.Core;

namespace torra_watch.UI.Controler
{
    public sealed class TickersList : UserControl
    {
        public DataGridView Grid { get; } = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        public TickersList()
        {
            Dock = DockStyle.Fill;
            var card = new CardPanel { Dock = DockStyle.Fill };
            card.Controls.Add(Grid);
            Controls.Add(card);
            StyleGrid(Grid);
        }

        public void Bind(IEnumerable<RankingRow> rows) =>
            Grid.DataSource = new BindingSource { DataSource = rows };

        public void ConfigureColumns(StrategyConfig cfg)
        {
            Grid.Columns.Clear();
            Grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Symbol", HeaderText = "Symbol", FillWeight = 130 });
            Grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PriceNow", HeaderText = "Now", FillWeight = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.########" } });
            Grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Price3hAgo", HeaderText = "3h Ago", FillWeight = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.########" } });
            Grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Ret3h", HeaderText = "3h %", FillWeight = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "P2" } });
            Grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "QuoteVol24h", HeaderText = "24h Quote Vol", FillWeight = 160, DefaultCellStyle = new DataGridViewCellStyle { Format = "0,0" } });

            Grid.CellFormatting += (s, e) =>
            {
                if (Grid.Columns[e.ColumnIndex].DataPropertyName == "Ret3h" && e.Value is decimal dec)
                    Grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = dec <= cfg.MinDrop3hPct ? Color.MistyRose : Color.White;
            };
        }

        private static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = UiTheme.Bg1;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Bg3;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            g.DefaultCellStyle.BackColor = UiTheme.Bg2;
            g.DefaultCellStyle.ForeColor = UiTheme.Text;
            g.DefaultCellStyle.SelectionBackColor = UiTheme.Blend(UiTheme.Accent, UiTheme.Bg2, 0.85);
            g.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
            g.GridColor = UiTheme.Stroke;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.RowTemplate.Height = 26;
        }
    }
}
