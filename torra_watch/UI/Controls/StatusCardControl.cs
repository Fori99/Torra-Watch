using torra_watch.UI.ViewModels;

namespace torra_watch.UI.Controls
{
    public partial class StatusCardControl : UserControl
    {
        private readonly Panel _dot = new();           // colored dot
        private readonly Label _lblStatus = new();     // “Running”, “Stopped”, etc.
        private readonly Label _lblCycle = new();     // step text / cooldown
        private readonly Label _lblMode = new();
        private readonly Label _lblUptime = new();
        private readonly Label _lblSignals = new();
        private readonly Label _lblOrders = new();
        private readonly FlowLayoutPanel _badges = new();

        public StatusCardControl()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(8);
            BackColor = Color.Transparent;

            var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 38, ColumnCount = 3 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));  // dot
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // primary text
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // badges

            // dot
            _dot.Width = _dot.Height = 14;
            _dot.Margin = new Padding(6, 10, 4, 0);
            _dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var b = new SolidBrush(_dot.BackColor);
                e.Graphics.FillEllipse(b, 0, 0, _dot.Width - 1, _dot.Height - 1);
            };

            // primary text
            _lblStatus.AutoSize = true;
            _lblStatus.Margin = new Padding(4, 8, 4, 0);
            _lblStatus.Font = new Font("Segoe UI Semibold", 10f);

            // badges (right)
            _badges.Dock = DockStyle.Fill;
            _badges.FlowDirection = FlowDirection.RightToLeft;
            _badges.WrapContents = false;
            _badges.Padding = new Padding(0);
            _badges.Margin = new Padding(0, 6, 6, 6);

            header.Controls.Add(_dot, 0, 0);
            header.Controls.Add(_lblStatus, 1, 0);
            header.Controls.Add(_badges, 2, 0);

            // cycle label
            _lblCycle.Dock = DockStyle.Top;
            _lblCycle.Margin = new Padding(6, 2, 6, 6);
            _lblCycle.Font = new Font("Segoe UI", 9f);
            _lblCycle.ForeColor = Color.FromArgb(72, 79, 86);
            _lblCycle.AutoSize = true;

            // stats grid
            var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(6, 0, 6, 6) };
            for (int i = 0; i < 3; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            stats.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            stats.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

            stats.Controls.Add(MakeKey("Mode"), 0, 0);
            stats.Controls.Add(MakeKey("Uptime"), 1, 0);
            stats.Controls.Add(MakeKey("Active orders"), 2, 0);

            _lblMode.Text = "-"; _lblUptime.Text = "00:00:00"; _lblOrders.Text = "0";
            stats.Controls.Add(_lblMode, 0, 1);
            stats.Controls.Add(_lblUptime, 1, 1);
            stats.Controls.Add(_lblOrders, 2, 1);

            // signals row
            var signalsRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Height = 22, Padding = new Padding(6, 0, 6, 0) };
            signalsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            signalsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            signalsRow.Controls.Add(MakeKey("Signals"), 0, 0);
            _lblSignals.Text = "0";
            signalsRow.Controls.Add(_lblSignals, 1, 0);

            Controls.Add(stats);
            Controls.Add(signalsRow);
            Controls.Add(_lblCycle);
            Controls.Add(header);
        }

        private static Label MakeKey(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(100, 107, 114),
            Font = new Font("Segoe UI", 9f)
        };

        // ---------- Public API ----------
        public void SetStatus(BotStatusVM vm)
        {
            // Dot color + caption
            (Color dot, string caption) = DotFor(vm.Primary);
            _dot.BackColor = dot;
            _dot.Invalidate();

            _lblStatus.Text = caption;

            // Cycle text (includes cooldown/step details)
            _lblCycle.Text = CycleText(vm);

            // stats
            _lblMode.Text = $"{vm.Mode} · {vm.Exchange}";
            _lblUptime.Text = vm.Uptime.ToString(@"dd\.hh\:mm\:ss").TrimStart('0', '.');
            _lblSignals.Text = vm.SignalsCount.ToString();
            _lblOrders.Text = vm.ActiveOrders.ToString();

            // badges
            _badges.Controls.Clear();
            if (vm.MarketClosed) _badges.Controls.Add(Badge("Market closed"));
            if (vm.Degraded) _badges.Controls.Add(Badge("Degraded"));
            if (vm.RateLimited) _badges.Controls.Add(Badge("Rate limited"));
            _badges.Controls.Add(Badge(vm.Connected ? "Connected" : "Connecting"));
        }

        private static (Color, string) DotFor(BotPrimaryStatus s) => s switch
        {
            BotPrimaryStatus.Running => (Color.FromArgb(0, 158, 73), "Running"),
            BotPrimaryStatus.CoolingDown => (Color.FromArgb(255, 193, 7), "Cooling down"),
            BotPrimaryStatus.Starting => (Color.FromArgb(255, 193, 7), "Starting…"),
            BotPrimaryStatus.Stopping => (Color.FromArgb(255, 159, 28), "Stopping…"),
            BotPrimaryStatus.Panic => (Color.FromArgb(255, 159, 28), "PANIC"),
            BotPrimaryStatus.Error => (Color.FromArgb(220, 53, 69), "Error"),
            BotPrimaryStatus.Stopped => (Color.FromArgb(220, 53, 69), "Stopped"),
            _ => (SystemColors.GrayText, s.ToString())
        };

        private static Control Badge(string text) => new Label
        {
            AutoSize = true,
            Text = text,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(72, 79, 86),
            BackColor = Color.FromArgb(238, 240, 242),
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(6, 0, 0, 0)
        };

        private static string CycleText(BotStatusVM v)
        {
            if (v.Primary == BotPrimaryStatus.CoolingDown && v.CooldownRemaining is TimeSpan cd)
                return $"Cooldown: {cd:mm\\:ss} left · then restart cycle";

            return v.Step switch
            {
                BotCycleStep.GetTopCoins => "Step 1/7: Get top coins",
                BotCycleStep.SortByChange => $"Step 2/7: Sort by {v.LookbackHours}h change (loss → profit)",
                BotCycleStep.PickSecondWorst => $"Step 3/7: Pick 2nd worst ≤ −{v.SecondWorstMinDropPct:N2}%",
                BotCycleStep.Buy => $"Step 4/7: Buy {v.CurrentSymbol ?? "coin"}",
                BotCycleStep.PlaceOco => $"Step 5/7: Place OCO (TP {v.TpPct:N2}% · SL {v.SlPct:N2}%)",
                BotCycleStep.WaitingToSell => "Step 6/7: Waiting to sell…",
                BotCycleStep.Restart => "Step 7/7: Restart cycle",
                _ => "Idle"
            };
        }
    }
}
