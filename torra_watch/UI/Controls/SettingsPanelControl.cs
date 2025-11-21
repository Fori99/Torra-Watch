using torra_watch.UI.ViewModels;


namespace torra_watch.UI.Controls
{
    /// <summary>
    /// Minimal settings panel for TorraWatch.
    /// Fields: Universe size, Min drop (3h) %, Take profit %, Stop loss %, Cooldown (minutes).
    /// Exposes SaveRequested and ResetRequested events.
    /// </summary>

    public partial class SettingsPanelControl : UserControl
    {
        // Events
        public event EventHandler<StrategyConfigVM>? SaveRequested;
        public event EventHandler? ResetRequested;

        // Inputs
        private readonly NumericUpDown _numUniverse = new();
        private readonly NumericUpDown _numMinDrop3h = new();
        private readonly NumericUpDown _numTP = new();
        private readonly NumericUpDown _numSL = new();
        private readonly NumericUpDown _numCooldownM = new();

        // Buttons
        private readonly Button _btnSave = new();
        private readonly Button _btnReset = new();

        public SettingsPanelControl()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(8);
            BackColor = Color.Transparent;

            SuspendLayout();
            BuildUi();
            ResumeLayout(true);

            // Load default values immediately
            ResetToDefaults();
        }

        // ---------------- UI ----------------

        private void BuildUi()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6, // 5 rows of inputs + 1 row of buttons
                Padding = new Padding(0)
            };

            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            for (int i = 0; i < 5; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // buttons

            // Labels
            grid.Controls.Add(MakeLabel("Universe size"), 0, 0);
            grid.Controls.Add(MakeLabel("Min drop (3h) %"), 0, 1);
            grid.Controls.Add(MakeLabel("Take profit %"), 0, 2);
            grid.Controls.Add(MakeLabel("Stop loss %"), 0, 3);
            grid.Controls.Add(MakeLabel("Cooldown (minutes)"), 0, 4);

            // Inputs
            InitInt(_numUniverse, min: 1, max: 1000, def: 150);
            InitPct(_numMinDrop3h, min: 0m, max: 100m, def: 4.00m);
            InitPct(_numTP, min: 0m, max: 100m, def: 2.00m);
            InitPct(_numSL, min: 0m, max: 100m, def: 2.00m);
            InitInt(_numCooldownM, min: 1, max: 240, def: 5);  // integer minutes

            grid.Controls.Add(_numUniverse, 1, 0);
            grid.Controls.Add(_numMinDrop3h, 1, 1);
            grid.Controls.Add(_numTP, 1, 2);
            grid.Controls.Add(_numSL, 1, 3);
            grid.Controls.Add(_numCooldownM, 1, 4);

            // Buttons row
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0)
            };

            StyleButton(_btnSave, "Save", Color.FromArgb(32, 134, 54), Color.White);
            StyleButton(_btnReset, "Reset", Color.FromArgb(108, 117, 125), Color.White);

            _btnSave.Click += (_, __) => SaveRequested?.Invoke(this, Read());
            _btnReset.Click += (_, __) => { ResetToDefaults(); ResetRequested?.Invoke(this, EventArgs.Empty); };

            buttons.Controls.Add(_btnSave);
            buttons.Controls.Add(_btnReset);

            grid.Controls.Add(buttons, 0, 5);
            grid.SetColumnSpan(buttons, 2);

            Controls.Add(grid);
        }

        // --------------- Public API ---------------

        public void LoadFrom(StrategyConfigVM vm)
        {
            _numUniverse.Value = Clamp(vm.UniverseSize, _numUniverse.Minimum, _numUniverse.Maximum);
            _numMinDrop3h.Value = Clamp(vm.MinDrop3hPct, _numMinDrop3h.Minimum, _numMinDrop3h.Maximum);
            _numTP.Value = Clamp(vm.TakeProfitPct, _numTP.Minimum, _numTP.Maximum);
            _numSL.Value = Clamp(vm.StopLossPct, _numSL.Minimum, _numSL.Maximum);
            _numCooldownM.Value = Clamp(vm.CooldownMinutes, _numCooldownM.Minimum, _numCooldownM.Maximum);
        }

        public StrategyConfigVM Read() => new StrategyConfigVM
        {
            UniverseSize = (int)_numUniverse.Value,
            MinDrop3hPct = _numMinDrop3h.Value,
            TakeProfitPct = _numTP.Value,
            StopLossPct = _numSL.Value,
            CooldownMinutes = (int)_numCooldownM.Value
        };

        public void ResetToDefaults() => LoadFrom(StrategyConfigVM.Defaults());

        // --------------- Helpers ---------------

        private static Label MakeLabel(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(60, 66, 72),
            Font = new Font("Segoe UI", 9f)
        };

        private static void InitInt(NumericUpDown n, int min, int max, int def)
        {
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, def));
            n.DecimalPlaces = 0;
            n.Increment = 1;
            n.Dock = DockStyle.Fill;
            n.Margin = new Padding(6, 4, 6, 4);
            n.Font = new Font("Segoe UI", 9f);
            n.TextAlign = HorizontalAlignment.Right;
            n.ThousandsSeparator = true;
        }

        private static void InitPct(NumericUpDown n, decimal min, decimal max, decimal def)
        {
            n.DecimalPlaces = 2;
            n.Increment = 0.10m;
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, def));
            n.Dock = DockStyle.Fill;
            n.Margin = new Padding(6, 4, 6, 4);
            n.Font = new Font("Segoe UI", 9f);
            n.TextAlign = HorizontalAlignment.Right;
        }

        private static decimal Clamp(decimal v, decimal min, decimal max) => Math.Min(max, Math.Max(min, v));
        private static decimal Clamp(int v, decimal min, decimal max) => Clamp((decimal)v, min, max);

        private static void StyleButton(Button b, string text, Color back, Color fore)
        {
            b.Text = text;
            b.AutoSize = false;
            b.Width = 92;
            b.Height = 30;
            b.Margin = new Padding(6, 6, 0, 6);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI Semibold", 9f);
            b.MouseEnter += (_, __) => b.BackColor = ControlPaint.Light(back, 0.2f);
            b.MouseLeave += (_, __) => b.BackColor = back;
        }
    }
}


