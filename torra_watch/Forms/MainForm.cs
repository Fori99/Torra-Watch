using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using torra_watch.Core;
using torra_watch.Services;
using torra_watch.UI.Controls;
using torra_watch.UI.ViewModels;

namespace torra_watch
{
    public partial class MainForm : Form
    {
        // ---------------- Services & config ----------------
        private readonly BotSettings _settings = BotSettings.LoadOrDefault();

        private readonly CancellationTokenSource _cts = new();
        private readonly IMarketDataService _market;
        private readonly ISettingsService _settingsSvc;
        private StrategyConfigVM _cfg;

        // ---------------- UI fields (promoted from locals) ----------------
        private TopCoinsListControl _topCoins;
        private ControlPanelControl _controlPanel;
        private SettingsPanelControl _settingsPanel;
        private StatusCardControl _statusCard;
        private SystemLogsControl _systemLogs;
        private PriceChartControl _priceChart;

        // ---------------- UI style tokens ----------------
        private static class Ui
        {
            public static readonly Color AppBg = Color.FromArgb(245, 247, 250);
            public static readonly Color MutedText = Color.FromArgb(100, 107, 114);
            public static readonly Color SecondaryText = Color.FromArgb(116, 124, 133);
            public const float TitleFontSize = 9f;
            public const float BodyFontSize = 9.5f;

            public static Padding OuterPadding => new(10);
            public static Padding CardMargin => new(6);
            public static Padding CardBodyPadding => new(10);
            public static Padding SectionPadding => new(8);
        }

        public MainForm()
        {
            InitializeComponent();
            BuildDashboardShell();

            var http = new HttpClient(); // single shared instance
            _market = new BinanceMarketDataService(http);
            _settingsSvc = new JsonSettingsService();
            _cfg = _settingsSvc.Load();

            // Bind settings panel
            _settingsPanel.LoadFrom(_cfg);
            _settingsPanel.SaveRequested += (_, vm) =>
            {
                var result = StrategyConfigValidator.Validate(vm);
                if (!result.Ok)
                {
                    MessageBox.Show(string.Join(Environment.NewLine, result.Errors),
                        "Invalid settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Persist and reflect in memory
                StrategyConfigMapper.ApplyToSettings(vm, _settings);
                _settings.Save();
                _cfg = vm;

                MessageBox.Show("Settings saved.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // Control panel events
            _controlPanel.StartRequested += async (_, __) =>
            {
                await RefreshTopCoins(); // immediate first run
                StartTimers();
                _statusCard.SetRunning(true);
            };
            _controlPanel.StopRequested += (_, __) =>
            {
                StopTimers();
                _statusCard.SetRunning(false);
            };
            _controlPanel.PanicRequested += (_, __) => MessageBox.Show("PANIC!");

            // Top coins row click -> chart
            _topCoins.SymbolSelected += symbol => LoadChart(symbol);
        }

        // ---------------- Layout ----------------
        private void BuildDashboardShell()
        {
            Text = "TorraWatch";
            BackColor = Ui.AppBg;
            WindowState = FormWindowState.Maximized;

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 4,
                RowCount = 3,
                Padding = Ui.OuterPadding
            };
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            Controls.Add(grid);

            // LEFT: Top Coins (spans all rows)
            var topCoinsCard = BuildTopCoinsSection();
            grid.Controls.Add(topCoinsCard, 0, 0);
            grid.SetRowSpan(topCoinsCard, 3);

            // ROW 0: Control Panel, Settings, Status
            var (controlPanelCard, settingsCard, statusCard) = BuildRow0_ControlsSettingsStatus();
            grid.Controls.Add(controlPanelCard, 1, 0);
            grid.Controls.Add(settingsCard, 2, 0);
            grid.Controls.Add(statusCard, 3, 0);

            // ROW 1: Logs + Account
            var logsCard = MakeCard("System Logs", "");
            _systemLogs = new SystemLogsControl { Dock = DockStyle.Fill };
            ReplaceCardBody(logsCard, _systemLogs);
            grid.Controls.Add(logsCard, 1, 1);
            grid.SetColumnSpan(logsCard, 2);

            var accountCard = MakeCard("Account Details", "");
            var account = new AccountPanelControl { Dock = DockStyle.Fill };
            account.Demo();
            ReplaceCardBody(accountCard, account);
            grid.Controls.Add(accountCard, 3, 1);

            // ROW 2: Trading Overview (orders + chart)
            var overviewCard = BuildRow2_TradingOverview();
            grid.Controls.Add(overviewCard, 1, 2);
            grid.SetColumnSpan(overviewCard, 3);
        }

        private (CardFrameControl, CardFrameControl, CardFrameControl) BuildRow0_ControlsSettingsStatus()
        {
            // Control Panel
            var controlPanelCard = MakeCard("Control Panel", "");
            _controlPanel = new ControlPanelControl { Dock = DockStyle.Fill };
            ReplaceCardBody(controlPanelCard, _controlPanel);

            // Settings
            var settingsCard = MakeCard("Settings", "");
            _settingsPanel = new SettingsPanelControl { Dock = DockStyle.Fill };
            ReplaceCardBody(settingsCard, _settingsPanel);

            // Status
            var statusCard = MakeCard("Status", "");
            _statusCard = new StatusCardControl { Dock = DockStyle.Fill };
            _statusCard.SetStatus(new BotStatusVM
            {
                Primary = BotPrimaryStatus.Running,
                Step = BotCycleStep.SortByChange,
                LookbackHours = Math.Max(1, _settings.LookbackMinutes / 60),
                SecondWorstMinDropPct = _settings.DropThresholdPct,
                TpPct = _settings.TpPct,
                SlPct = _settings.SlPct,
                Mode = _settings.Mode.ToString(),
                Exchange = "Binance",
                Connected = true,
                Uptime = TimeSpan.FromMinutes(12)
            });
            ReplaceCardBody(statusCard, _statusCard);

            return (controlPanelCard, settingsCard, statusCard);
        }

        private CardFrameControl BuildTopCoinsSection()
        {
            var topCoinsCard = MakeCard("Top Coins", "");
            topCoinsCard.BorderColor = Color.FromArgb(239, 240, 242);
            topCoinsCard.BorderThickness = 1;
            topCoinsCard.CornerRadius = 0;

            _topCoins = new TopCoinsListControl { Dock = DockStyle.Fill };
            _topCoins.SetWindowHours(Math.Max(1, _settings.LookbackMinutes / 60));
            _topCoins.SetCoins(SeedDemoTopCoins(), Math.Max(1, _settings.LookbackMinutes / 60));

            ReplaceCardBody(topCoinsCard, _topCoins);
            return topCoinsCard;
        }

        private CardFrameControl BuildRow2_TradingOverview()
        {
            var overview = new CardFrameControl
            {
                Dock = DockStyle.Fill,
                Title = "Trading Overview",
                Margin = Ui.SectionPadding
            };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = Ui.SectionPadding,
                BackColor = Color.Transparent
            };
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333f));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333f));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333f));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            overview.Body.Controls.Clear();
            overview.Body.Controls.Add(inner);

            // LEFT: Live Orders
            var ordersCard = MakeHeaderlessCard("Live Orders", out var ordersHeaderLabel);
            var liveOrders = new LiveOrdersListControl { Dock = DockStyle.Fill, ShowHeader = false };
            liveOrders.Demo();
            ordersCard.Body.Controls.Add(liveOrders);
            ordersCard.Body.Controls.Add(ordersHeaderLabel);
            inner.Controls.Add(ordersCard, 0, 0);

            // MIDDLE+RIGHT: Chart
            var chartCard = MakeHeaderlessCard("Price Chart", out var chartHeaderLabel, margin: new Padding(8, 0, 0, 0));
            _priceChart = new PriceChartControl { Dock = DockStyle.Fill };
            chartCard.Body.Controls.Add(_priceChart);
            chartCard.Body.Controls.Add(chartHeaderLabel);
            inner.Controls.Add(chartCard, 1, 0);
            inner.SetColumnSpan(chartCard, 2);

            return overview;
        }

        // ---------------- Helpers ----------------
        private static CardFrameControl MakeCard(string title, string placeholder)
        {
            var card = new CardFrameControl { Dock = DockStyle.Fill, Title = title, Margin = Ui.CardMargin };
            var label = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Text = placeholder,
                Font = new Font("Segoe UI", Ui.BodyFontSize),
                ForeColor = Color.FromArgb(90, 100, 110),
                Padding = new Padding(4, 2, 4, 8),
                TextAlign = ContentAlignment.TopLeft
            };
            card.Body.Padding = Ui.CardBodyPadding;
            card.Body.Controls.Add(label);
            return card;
        }

        private static CardFrameControl MakeHeaderlessCard(string titleText, out Label headerLabel, Padding? margin = null)
        {
            var card = new CardFrameControl { Dock = DockStyle.Fill, HeaderVisible = false, Margin = margin ?? Padding.Empty };
            headerLabel = new Label
            {
                Text = titleText,
                Dock = DockStyle.Top,
                Height = 22,
                Padding = new Padding(0, 0, 0, 6),
                Font = new Font("Segoe UI Semibold", Ui.TitleFontSize),
                ForeColor = Ui.SecondaryText
            };
            card.Body.Padding = Ui.CardBodyPadding;
            return card;
        }

        private static void ReplaceCardBody(CardFrameControl card, Control content)
        {
            card.Body.Controls.Clear();
            content.Dock = DockStyle.Fill;
            card.Body.Controls.Add(content);
        }

        private static TopCoinVM[] SeedDemoTopCoins() => new[]
        {
            new TopCoinVM { Symbol = "BTC",  Price = 42150m, ChangePct =  2.5m },
            new TopCoinVM { Symbol = "ETH",  Price =  2250m, ChangePct =  1.8m },
            new TopCoinVM { Symbol = "BNB",  Price =   312m, ChangePct = -0.5m },
            new TopCoinVM { Symbol = "SOL",  Price =    98m, ChangePct =  5.2m },
            new TopCoinVM { Symbol = "XRP",  Price =   0.6m, ChangePct =  0.7m },
            new TopCoinVM { Symbol = "ADA",  Price =  0.52m, ChangePct =  0.8m },
            new TopCoinVM { Symbol = "DOGE", Price =  0.12m, ChangePct =  1.1m },
            new TopCoinVM { Symbol = "LTC",  Price =     78m, ChangePct =  0.4m },
            new TopCoinVM { Symbol = "DOT",  Price =    6.2m, ChangePct =  1.6m },
            new TopCoinVM { Symbol = "AVAX", Price =     25m, ChangePct = -0.9m },
        };

        // ---------------- Timers ----------------
        private System.Windows.Forms.Timer? _scanTimer;
        private void StartTimers()
        {
            _scanTimer ??= new System.Windows.Forms.Timer { Interval = 60_000 };
            _scanTimer.Tick -= ScanTick;
            _scanTimer.Tick += ScanTick;
            _scanTimer.Start();
        }
        private void StopTimers() => _scanTimer?.Stop();
        private async void ScanTick(object? s, EventArgs e) => await RefreshTopCoins();

        // ---------------- Data pushes ----------------
        private async Task RefreshTopCoins()
        {
            try
            {
                var lookbackHours = Math.Max(1, _settings.LookbackMinutes / 60);
                var lookback = TimeSpan.FromHours(lookbackHours);

                var movers = await _market.GetTopMoversAsync(_cfg.UniverseSize, lookback, _cts.Token);
                var vms = movers.Select(m => new TopCoinVM
                {
                    Symbol = m.Symbol,
                    Price = m.Now,
                    ChangePct = m.ChangePct
                }).ToArray();

                void Apply() => _topCoins.SetCoins(vms, lookbackHours);
                if (InvokeRequired) BeginInvoke((Action)Apply); else Apply();
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"❌ RefreshTopCoins: {ex.Message}");
            }
        }

        private async void LoadChart(string symbol)
        {
            try
            {
                var candles = await _market.GetKlinesAsync(symbol, "1m", 180, _cts.Token);
                void Apply() => _priceChart.Draw(symbol, candles);
                if (InvokeRequired) BeginInvoke((Action)Apply); else Apply();
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"❌ LoadChart: {ex.Message}");
            }
        }
    }
}
