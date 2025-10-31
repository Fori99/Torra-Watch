using System;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using torra_watch.Core;
using torra_watch.Models;            // Position, Balance, OpenOrder
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
        private IAccountService? _account;

        private StrategyConfigVM _cfg = StrategyConfigVM.Defaults();

        // ---------------- UI fields ----------------
        private TopCoinsListControl _topCoins = null!;
        private ControlPanelControl _controlPanel = null!;
        private SettingsPanelControl _settingsPanel = null!;
        private StatusCardControl _statusCard = null!;
        private SystemLogsControl _systemLogs = null!;
        private PriceChartControl _priceChart = null!;
        private AccountPanelControl _accountPanel = null!;

        // timers
        private System.Windows.Forms.Timer? _scanTimer;
        private System.Windows.Forms.Timer? _accountTimer;

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

            // Services
            var http = new HttpClient();
            _market = new BinanceMarketDataService(http);

            var httpPublic = new HttpClient();

            // NOW check credentials AFTER _systemLogs exists
            if (BinanceCreds.TryRead(out var key, out var secret, out var mode, _systemLogs))
            {
                var httpSigned = new HttpClient();
                var signed = new BinanceSignedClient(httpSigned, key, secret, mode, _systemLogs); // Pass logger
                _account = new BinanceAccountService(signed, httpPublic);
            }

            _settingsSvc = new JsonSettingsService();
            _cfg = _settingsSvc.Load();

            // Settings panel
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

                StrategyConfigMapper.ApplyToSettings(vm, _settings);
                _settings.Save();
                _cfg = vm;
                MessageBox.Show("Settings saved.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // Control panel events
            _controlPanel.StartRequested += async (_, __) =>  TestBinanceConnection();
            _controlPanel.StopRequested += (_, __) => StopAll();
            _controlPanel.PanicRequested += (_, __) => MessageBox.Show("PANIC!");

            // Top coins → chart
            //_topCoins.SymbolSelected += symbol => LoadChart(symbol);

            // Initial status (stopped)
            UpdateStatus(running: false);
        }

        // Add this test method to MainForm
        private async void TestBinanceConnection()
        {
            try
            {
                _systemLogs?.Append("Testing Binance API connection...", LogLevel.Info);

                var key = Environment.GetEnvironmentVariable("TORRA_BINANCE_KEY") ?? "";
                var secret = Environment.GetEnvironmentVariable("TORRA_BINANCE_SECRET") ?? "";

                using var http = new HttpClient();
                http.BaseAddress = new Uri("https://testnet.binance.vision");
                http.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var queryString = $"timestamp={timestamp}";

                using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(queryString));
                var signature = BitConverter.ToString(hash).Replace("-", "").ToLower();

                var url = $"/api/v3/account?{queryString}&signature={signature}";

                _systemLogs?.Append($"Testing URL: {url}", LogLevel.Debug);

                var response = await http.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                _systemLogs?.Append($"Status: {response.StatusCode}", LogLevel.Info);
                _systemLogs?.Append($"Response: {body}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"Test failed: {ex.Message}", LogLevel.Error);
            }
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
            _accountPanel = new AccountPanelControl { Dock = DockStyle.Fill };
            ReplaceCardBody(accountCard, _accountPanel);
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

            var lookbackH = Math.Max(1, _settings.LookbackMinutes / 60);
            _topCoins.SetWindowHours(lookbackH);
            _topCoins.SetCoins(SeedDemoTopCoins(), lookbackH);

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

        // ---------------- Start/Stop orchestration ----------------
        private async Task StartAllAsync()
        {
            try
            {
                await RefreshTopCoins();

                if (_account != null)
                {
                    await RefreshAccount();
                    StartAccountTimer();
                }
                else
                {
                    _systemLogs?.Append("Binance API keys not found. Running without account sync.");
                }

                StartScanTimer();
                UpdateStatus(running: true);
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"❌ Start: {ex.Message}");
            }
        }

        private void StopAll()
        {
            StopScanTimer();
            StopAccountTimer();
            UpdateStatus(running: false);
        }

        private void UpdateStatus(bool running)
        {
            _statusCard.SetStatus(new BotStatusVM
            {
                Primary = running ? BotPrimaryStatus.Running : BotPrimaryStatus.Stopped,
                Step = running ? BotCycleStep.SortByChange : BotCycleStep.Restart,
                LookbackHours = Math.Max(1, _settings.LookbackMinutes / 60),
                SecondWorstMinDropPct = _settings.DropThresholdPct,
                TpPct = _settings.TpPct,
                SlPct = _settings.SlPct,
                Mode = _settings.Mode.ToString(),
                Exchange = "Binance",
                Connected = _account != null,
                Uptime = running ? TimeSpan.FromMinutes(12) : TimeSpan.Zero
            });
        }

        // ---------------- Timers ----------------
        private void StartScanTimer()
        {
            _scanTimer ??= new System.Windows.Forms.Timer { Interval = 60_000 };
            _scanTimer.Tick -= ScanTick;
            _scanTimer.Tick += ScanTick;
            _scanTimer.Start();
        }
        private void StopScanTimer() => _scanTimer?.Stop();
        private async void ScanTick(object? s, EventArgs e) => await RefreshTopCoins();

        private void StartAccountTimer()
        {
            _accountTimer ??= new System.Windows.Forms.Timer { Interval = 15_000 };
            _accountTimer.Tick -= AccountTick;
            _accountTimer.Tick += AccountTick;
            _accountTimer.Start();
        }
        private void StopAccountTimer() => _accountTimer?.Stop();
        private async void AccountTick(object? s, EventArgs e) => await RefreshAccount();

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

        private async Task RefreshAccount()
        {
            if (_account == null) return;

            try
            {
                var snap = await _account.GetBalancesAsync(_cts.Token);
                var openOrders = await _account.GetOpenOrdersAsync(_cts.Token); // IReadOnlyList<OpenOrder>

                // Map OpenOrder -> Position (group per symbol, derive Entry/TP/SL)
                var positions = openOrders
                    .GroupBy(o => o.Symbol, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        decimal entry = g.Where(o => string.Equals(o.Side, "BUY", StringComparison.OrdinalIgnoreCase))
                                         .Select(o => o.Price).DefaultIfEmpty(0m).First();

                        decimal tp = g.Where(o => string.Equals(o.Side, "SELL", StringComparison.OrdinalIgnoreCase) &&
                                                  (o.Type.Contains("TAKE_PROFIT", StringComparison.OrdinalIgnoreCase) ||
                                                   o.Type.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)))
                                      .Select(o => o.Price).DefaultIfEmpty(0m).First();

                        decimal sl = g.Where(o => o.Type.Contains("STOP", StringComparison.OrdinalIgnoreCase))
                                      .Select(o => o.Price).DefaultIfEmpty(0m).First();

                        return new Models.Position { Symbol = g.Key, Price = entry, TakeProfit = tp, StopLoss = sl };
                    })
                    .ToList();

                void Apply()
                {
                    _accountPanel.SetTotalUsdt(snap.TotalUsdt);
                    _accountPanel.SetBalances(snap.Balances);
                    _accountPanel.SetOpenOrders(positions);
                }

                if (InvokeRequired) BeginInvoke((Action)Apply); else Apply();
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"❌ RefreshAccount: {ex.Message}");
            }
        }

        private async void LoadChart(string symbol)
        {
            try
            {
                var candles = await _market.GetKlinesAsync(symbol, "1m", 180, _cts.Token);

                // Uncomment this when PriceChartControl has Draw(string, IReadOnlyList<Candle>) implemented
                // void Apply() => _priceChart.Draw(symbol, candles);
                // if (InvokeRequired) BeginInvoke((Action)Apply); else Apply();
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"❌ LoadChart: {ex.Message}");
            }
        }

        // ---------------- Creds helper ----------------
        // Replace your BinanceCreds class with this version:

        // Replace your BinanceCreds class with this version:

        public static class BinanceCreds
        {
            public static bool TryRead(out string key, out string secret, out string mode, SystemLogsControl? logger = null)
            {
                key = Environment.GetEnvironmentVariable("TORRA_BINANCE_KEY") ?? "";
                secret = Environment.GetEnvironmentVariable("TORRA_BINANCE_SECRET") ?? "";
                mode = (Environment.GetEnvironmentVariable("TORRA_BINANCE_MODE") ?? "live").ToLowerInvariant();

                // Log what we found
                logger?.Append("Checking Binance credentials...", LogLevel.Info);
                logger?.Append($"TORRA_BINANCE_KEY: {(string.IsNullOrWhiteSpace(key) ? "NOT SET" : $"SET (length: {key.Length})")}", LogLevel.Debug);
                logger?.Append($"TORRA_BINANCE_SECRET: {(string.IsNullOrWhiteSpace(secret) ? "NOT SET" : $"SET (length: {secret.Length})")}", LogLevel.Debug);
                logger?.Append($"TORRA_BINANCE_MODE: {mode}", LogLevel.Debug);

                // Optional fallback to BotSettings if you store them there
                try
                {
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
                    {
                        logger?.Append("Environment variables not found, trying BotSettings fallback...", LogLevel.Warning);
                        var s = BotSettings.LoadOrDefault();

                        // Uncomment these if you want to use BotSettings fallback:
                        // if (string.IsNullOrWhiteSpace(key)) key = s.BinanceApiKey ?? "";
                        // if (string.IsNullOrWhiteSpace(secret)) secret = s.BinanceApiSecret ?? "";
                        // if (string.IsNullOrWhiteSpace(mode)) mode = s.BinanceMode ?? "live";

                        logger?.Append($"BotSettings - Key: {(string.IsNullOrWhiteSpace(key) ? "NOT SET" : "SET")}", LogLevel.Debug);
                        logger?.Append($"BotSettings - Secret: {(string.IsNullOrWhiteSpace(secret) ? "NOT SET" : "SET")}", LogLevel.Debug);
                    }
                }
                catch (Exception ex)
                {
                    logger?.Append($"Error loading BotSettings: {ex.Message}", LogLevel.Error);
                }

                bool result = !(string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret));

                if (!result)
                {
                    logger?.Append("Binance API keys not found. Running without account sync.", LogLevel.Warning);
                }
                else
                {
                    var modeDisplay = mode switch
                    {
                        "demo" => "Demo (demo.binance.com)",
                        "testnet" => "Testnet",
                        _ => "Live Production"
                    };
                    logger?.Append($"Binance credentials loaded successfully (Mode: {modeDisplay})", LogLevel.Success);
                }

                return result;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _scanTimer?.Dispose();
                _accountTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
