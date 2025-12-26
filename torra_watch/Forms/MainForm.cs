using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        private IAccountService? _account;
        private readonly ExchangeInfoCache _exchangeInfoCache;

        private StrategyConfigVM _cfg = StrategyConfigVM.Defaults();

        // ---------------- UI fields ----------------
        private TopCoinsListControl _topCoins = null!;
        private ControlPanelControl _controlPanel = null!;
        private SettingsPanelControl _settingsPanel = null!;
        private StatusCardControl _statusCard = null!;
        private SystemLogsControl _systemLogs = null!;
        private PriceChartControl _priceChart = null!;
        private AccountPanelControl _accountPanel = null!;

        // timers - ⭐ ADDED TRADE TIMER
        private System.Windows.Forms.Timer? _scanTimer;
        private System.Windows.Forms.Timer? _accountTimer;
        private System.Windows.Forms.Timer? _tradeTimer;  // ⭐ NEW

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
            _exchangeInfoCache = new ExchangeInfoCache(_market, TimeSpan.FromHours(4));

            var httpPublic = new HttpClient();

            // NOW check credentials AFTER _systemLogs exists
            if (BinanceCreds.TryRead(out var key, out var secret, out var mode, _systemLogs))
            {
                var httpSigned = new HttpClient();
                var signed = new BinanceSignedClient(httpSigned, key, secret, mode, _systemLogs);
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
            _controlPanel.StartRequested += async (_, __) => await StartAllAsync();
            _controlPanel.StopRequested += (_, __) => StopAll();
            _controlPanel.PanicRequested += (_, __) => DiagnoseAccountMismatch();

            // Initial status (stopped)
            UpdateStatus(running: false);
        }

        // ---------------- Layout (unchanged) ----------------
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

            var topCoinsCard = BuildTopCoinsSection();
            grid.Controls.Add(topCoinsCard, 0, 0);
            grid.SetRowSpan(topCoinsCard, 3);

            var (controlPanelCard, settingsCard, statusCard) = BuildRow0_ControlsSettingsStatus();
            grid.Controls.Add(controlPanelCard, 1, 0);
            grid.Controls.Add(settingsCard, 2, 0);
            grid.Controls.Add(statusCard, 3, 0);

            var logsCard = MakeCard("System Logs", "");
            _systemLogs = new SystemLogsControl { Dock = DockStyle.Fill };
            ReplaceCardBody(logsCard, _systemLogs);
            grid.Controls.Add(logsCard, 1, 1);
            grid.SetColumnSpan(logsCard, 2);

            var accountCard = MakeCard("Account Details", "");
            _accountPanel = new AccountPanelControl { Dock = DockStyle.Fill };
            ReplaceCardBody(accountCard, _accountPanel);
            grid.Controls.Add(accountCard, 3, 1);

            var overviewCard = BuildRow2_TradingOverview();
            grid.Controls.Add(overviewCard, 1, 2);
            grid.SetColumnSpan(overviewCard, 3);
        }

        private (CardFrameControl, CardFrameControl, CardFrameControl) BuildRow0_ControlsSettingsStatus()
        {
            var controlPanelCard = MakeCard("Control Panel", "");
            _controlPanel = new ControlPanelControl { Dock = DockStyle.Fill };
            ReplaceCardBody(controlPanelCard, _controlPanel);

            var settingsCard = MakeCard("Settings", "");
            _settingsPanel = new SettingsPanelControl { Dock = DockStyle.Fill };
            ReplaceCardBody(settingsCard, _settingsPanel);

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

            var ordersCard = MakeHeaderlessCard("Live Orders", out var ordersHeaderLabel);
            var liveOrders = new LiveOrdersListControl { Dock = DockStyle.Fill, ShowHeader = false };
            liveOrders.Demo();
            ordersCard.Body.Controls.Add(liveOrders);
            ordersCard.Body.Controls.Add(ordersHeaderLabel);
            inner.Controls.Add(ordersCard, 0, 0);

            var chartCard = MakeHeaderlessCard("Price Chart", out var chartHeaderLabel, margin: new Padding(8, 0, 0, 0));
            _priceChart = new PriceChartControl { Dock = DockStyle.Fill };
            chartCard.Body.Controls.Add(_priceChart);
            chartCard.Body.Controls.Add(chartHeaderLabel);
            inner.Controls.Add(chartCard, 1, 0);
            inner.SetColumnSpan(chartCard, 2);

            return overview;
        }

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

        // ---------------- Start/Stop orchestration - ⭐ UPDATED ----------------
        private async Task StartAllAsync()
        {
            try
            {
                _systemLogs?.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Info);
                _systemLogs?.Append("🚀 Starting Automated Trading Bot", LogLevel.Info);
                _systemLogs?.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Info);

                await RefreshTopCoins();

                if (_account != null)
                {
                    await RefreshAccount();

                    // Check if we already have open positions
                    var openOrders = await _account.GetOpenOrdersAsync(_cts.Token);

                    if (openOrders.Count == 0)
                    {
                        _systemLogs?.Append("💼 No open positions - executing initial trade", LogLevel.Info);
                        await ExecuteTradeAsync();
                    }
                    else
                    {
                        _systemLogs?.Append($"⏸️ Skipping initial trade - {openOrders.Count} open order(s) exist", LogLevel.Warning);
                        foreach (var order in openOrders.Take(3))
                        {
                            _systemLogs?.Append($"   → {order.Symbol}: {order.Type} {order.Side}", LogLevel.Info);
                        }
                    }

                    StartAccountTimer();
                    StartTradeTimer();  // ⭐ START TRADE TIMER
                }
                else
                {
                    _systemLogs?.Append("❌ Binance API keys not found. Running without account sync.", LogLevel.Warning);
                }

                StartScanTimer();
                UpdateStatus(running: true);

                _systemLogs?.Append($"", LogLevel.Info);
                _systemLogs?.Append($"✅ Bot started successfully!", LogLevel.Success);
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"❌ Start: {ex.Message}", LogLevel.Error);
            }
        }

        private void StopAll()
        {
            _systemLogs?.Append("", LogLevel.Info);
            _systemLogs?.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Warning);
            _systemLogs?.Append("⏹️ Stopping Bot...", LogLevel.Warning);
            _systemLogs?.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Warning);

            StopScanTimer();
            StopAccountTimer();
            StopTradeTimer();  // ⭐ STOP TRADE TIMER
            UpdateStatus(running: false);

            _systemLogs?.Append("✅ Bot stopped", LogLevel.Success);
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

        // ---------------- Timers - ⭐ ADDED TRADE TIMER ----------------
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

        // ⭐ NEW: Trade timer for automated loop
        private void StartTradeTimer()
        {
            var cooldownMs = _settings.CooldownMinutes * 60_000;

            _tradeTimer ??= new System.Windows.Forms.Timer { Interval = cooldownMs };
            _tradeTimer.Tick -= TradeTick;
            _tradeTimer.Tick += TradeTick;
            _tradeTimer.Start();

            _systemLogs?.Append($"", LogLevel.Info);
            _systemLogs?.Append($"⏱️ Trade timer started", LogLevel.Success);
            _systemLogs?.Append($"   Interval: {_settings.CooldownMinutes} minutes", LogLevel.Info);
            _systemLogs?.Append($"   Next check: {DateTime.Now.AddMinutes(_settings.CooldownMinutes):HH:mm:ss}", LogLevel.Info);
        }

        private void StopTradeTimer()
        {
            _tradeTimer?.Stop();
            _systemLogs?.Append("⏱️ Trade timer stopped", LogLevel.Info);
        }

        // ⭐ NEW: Trade tick - checks for closed positions and executes new trades
        private async void TradeTick(object? s, EventArgs e)
        {
            try
            {
                _systemLogs?.Append($"", LogLevel.Info);
                _systemLogs?.Append($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Info);
                _systemLogs?.Append($"⏰ Trade Timer: {DateTime.Now:HH:mm:ss}", LogLevel.Info);
                _systemLogs?.Append($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Info);

                if (_account == null)
                {
                    _systemLogs?.Append("❌ No account connection", LogLevel.Error);
                    return;
                }

                // Check for open positions
                _systemLogs?.Append($"🔍 Checking for open positions...", LogLevel.Info);
                var openOrders = await _account.GetOpenOrdersAsync(_cts.Token);

                if (openOrders.Count > 0)
                {
                    _systemLogs?.Append($"⏸️ {openOrders.Count} position(s) still open - waiting", LogLevel.Warning);

                    foreach (var order in openOrders.Take(3))
                    {
                        _systemLogs?.Append($"   → {order.Symbol}: {order.Type} {order.Side} @ ${order.Price}", LogLevel.Info);
                    }

                    _systemLogs?.Append($"   Next check: {DateTime.Now.AddMinutes(_settings.CooldownMinutes):HH:mm:ss}", LogLevel.Info);
                    return;
                }

                _systemLogs?.Append($"✅ No open positions detected", LogLevel.Success);
                _systemLogs?.Append($"🔄 Starting new trade cycle...", LogLevel.Info);

                // Refresh data
                await RefreshAccount();
                await RefreshTopCoins();

                // Execute trade
                await ExecuteTradeAsync();

                _systemLogs?.Append($"", LogLevel.Info);
                _systemLogs?.Append($"📅 Next trade check: {DateTime.Now.AddMinutes(_settings.CooldownMinutes):HH:mm:ss}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"", LogLevel.Error);
                _systemLogs?.Append($"❌ Trade tick error: {ex.Message}", LogLevel.Error);
                _systemLogs?.Append($"   {ex.StackTrace}", LogLevel.Debug);
            }
        }

        // ---------------- Data pushes ----------------
        private async Task RefreshTopCoins()
        {
            try
            {
                var lookbackHours = Math.Max(1, _settings.LookbackMinutes / 60);
                var lookback = TimeSpan.FromHours(lookbackHours);

                var movers = await _market.GetTopMoversAsync(_cfg.UniverseSize, lookback, _cts.Token);

                var vms = movers
                    .OrderBy(m => m.ChangePct)
                    .Select(m => new TopCoinVM
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
                _systemLogs?.Append($"❌ RefreshTopCoins: {ex.Message}", LogLevel.Error);
            }
        }

        private async Task RefreshAccount()
        {
            if (_account == null) return;

            try
            {
                var snap = await _account.GetBalancesAsync(_cts.Token);
                var openOrders = await _account.GetOpenOrdersAsync(_cts.Token);

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
                _systemLogs?.Append($"❌ RefreshAccount: {ex.Message}", LogLevel.Error);
            }
        }

        // ============================================
        // ⭐ AUTOMATED TRADING LOGIC
        // ============================================

        private async Task ExecuteTradeAsync()
        {
            if (_account == null)
            {
                _systemLogs?.Append("❌ Cannot trade: No account connection", LogLevel.Error);
                return;
            }

            try
            {
                _systemLogs?.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Info);
                _systemLogs?.Append("🤖 Starting Automated Trade...", LogLevel.Info);
                _systemLogs?.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Info);

                var lookbackHours = Math.Max(1, _settings.LookbackMinutes / 60);
                var lookback = TimeSpan.FromHours(lookbackHours);

                var movers = await _market.GetTopMoversAsync(_cfg.UniverseSize, lookback, _cts.Token);

                if (movers.Count < 2)
                {
                    _systemLogs?.Append("❌ Not enough coins to trade (need at least 2)", LogLevel.Error);
                    return;
                }

                var targetCoin = movers[1];
                var symbol = targetCoin.Symbol + "USDT";
                var currentPrice = targetCoin.Now;

                _systemLogs?.Append($"", LogLevel.Info);
                _systemLogs?.Append($"🎯 Target: {targetCoin.Symbol} at ${currentPrice:N6} ({targetCoin.ChangePct:N2}%)", LogLevel.Success);

                // Use cached exchange info (refreshed every 4 hours)
                var symbolInfo = await _exchangeInfoCache.GetSymbolInfoAsync(symbol, _cts.Token);
                if (symbolInfo == null)
                {
                    _systemLogs?.Append($"❌ Could not get trading rules for {symbol}", LogLevel.Error);
                    return;
                }

                _systemLogs?.Append($"📋 Symbol filters: stepSize={symbolInfo.StepSize}, minQty={symbolInfo.MinQty}, minNotional={symbolInfo.MinNotional}", LogLevel.Debug);

                var accountSnap = await _account.GetBalancesAsync(_cts.Token);
                var usdtBalance = accountSnap.Balances
                    .FirstOrDefault(b => b.Asset.Equals("USDT", StringComparison.OrdinalIgnoreCase))
                    ?.Qty ?? 0m;

                if (usdtBalance < symbolInfo.MinNotional)
                {
                    _systemLogs?.Append($"❌ Insufficient USDT: ${usdtBalance:N2} (min: ${symbolInfo.MinNotional:N2})", LogLevel.Error);
                    return;
                }

                // Calculate buy quantity using helper - rounds DOWN to stepSize
                var usdtToSpend = usdtBalance * 0.99m; // Use 99% to leave room for fees
                var rawQuantity = usdtToSpend / currentPrice;

                var buyResult = QuantityHelper.AdjustBuyQuantity(rawQuantity, currentPrice, symbolInfo);
                if (!buyResult.IsSuccess)
                {
                    _systemLogs?.Append($"❌ Cannot buy: {buyResult.ErrorMessage}", LogLevel.Error);
                    return;
                }

                var quantity = buyResult.Quantity;
                _systemLogs?.Append($"🛒 Placing MARKET BUY: {quantity} {targetCoin.Symbol} (~${buyResult.Notional:N2} USDT)", LogLevel.Warning);

                var buyOrder = await _account.PlaceMarketBuyAsync(symbol, quantity, _cts.Token);

                if (buyOrder == null)
                {
                    _systemLogs?.Append("❌ Buy order failed", LogLevel.Error);
                    return;
                }

                _systemLogs?.Append($"✅ BUY FILLED: {buyOrder.ExecutedQty} @ ${buyOrder.AvgPrice:N6}", LogLevel.Success);

                var entryPrice = buyOrder.AvgPrice;

                _systemLogs?.Append($"⏳ Waiting for balance to settle...", LogLevel.Warning);
                await Task.Delay(3000, _cts.Token);

                // Get ACTUAL balance for OCO - this is key to preventing dust!
                // We sell the ENTIRE balance, not the bought quantity
                int retryCount = 0;
                decimal actualCoinBalance = 0m;

                while (retryCount < 3)
                {
                    actualCoinBalance = await _account.GetAssetBalanceAsync(targetCoin.Symbol, _cts.Token);

                    if (actualCoinBalance >= buyOrder.ExecutedQty * 0.99m) break;

                    retryCount++;
                    _systemLogs?.Append($"   Retry {retryCount}/3 - Balance: {actualCoinBalance}", LogLevel.Debug);
                    if (retryCount < 3) await Task.Delay(2000, _cts.Token);
                }

                _systemLogs?.Append($"💰 Actual {targetCoin.Symbol} balance: {actualCoinBalance}", LogLevel.Info);

                // Round DOWN the ACTUAL balance to get sellable quantity - minimizes dust!
                var sellableQty = QuantityHelper.FloorToStepSize(actualCoinBalance, symbolInfo.StepSize);
                var dustAfterSell = actualCoinBalance - sellableQty;

                if (sellableQty <= 0 || sellableQty < symbolInfo.MinQty)
                {
                    _systemLogs?.Append($"❌ Cannot create OCO: sellable qty {sellableQty} < min {symbolInfo.MinQty}", LogLevel.Error);
                    return;
                }

                _systemLogs?.Append($"📊 Will sell: {sellableQty} (dust remaining: {dustAfterSell})", LogLevel.Info);

                // Calculate exit prices using helper - properly rounded
                var (takeProfitPrice, stopLossPrice, stopLimitPrice) = QuantityHelper.CalculateExitPrices(
                    entryPrice,
                    _settings.TpPct,
                    _settings.SlPct,
                    symbolInfo);

                _systemLogs?.Append($"📝 Placing OCO with ENTIRE sellable balance:", LogLevel.Warning);
                _systemLogs?.Append($"   Quantity: {sellableQty} {targetCoin.Symbol}", LogLevel.Info);
                _systemLogs?.Append($"   Take Profit: ${takeProfitPrice:N6} (+{_settings.TpPct}%)", LogLevel.Info);
                _systemLogs?.Append($"   Stop Loss: ${stopLossPrice:N6} (-{_settings.SlPct}%)", LogLevel.Info);

                await _account.PlaceOcoOrderAsync(symbol, sellableQty, takeProfitPrice, stopLossPrice, stopLimitPrice, _cts.Token);

                _systemLogs?.Append($"✅ OCO PLACED! Trade complete.", LogLevel.Success);
                if (dustAfterSell > 0)
                {
                    _systemLogs?.Append($"⚠️ Dust remaining: {dustAfterSell} {targetCoin.Symbol} (below stepSize)", LogLevel.Warning);
                }
                _systemLogs?.Append($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", LogLevel.Info);

                await RefreshAccount();
            }
            catch (Exception ex)
            {
                _systemLogs?.Append($"❌ TRADE ERROR: {ex.Message}", LogLevel.Error);
            }
        }

        // ---------------- Diagnostics (for testing) ----------------
        private async void DiagnoseAccountMismatch()
        {
            _systemLogs?.Append("=== ACCOUNT DIAGNOSIS ===", LogLevel.Info);

            var key = Environment.GetEnvironmentVariable("TORRA_BINANCE_KEY") ?? "";
            var secret = Environment.GetEnvironmentVariable("TORRA_BINANCE_SECRET") ?? "";

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            {
                _systemLogs?.Append("No API keys found!", LogLevel.Error);
                return;
            }

            var endpoints = new[]
            {
                ("LIVE", "https://api.binance.com"),
                ("TESTNET", "https://testnet.binance.vision")
            };

            foreach (var (name, baseUrl) in endpoints)
            {
                _systemLogs?.Append($"\n--- Testing {name} ---", LogLevel.Info);

                try
                {
                    using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };
                    http.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);

                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var queryString = $"timestamp={timestamp}";

                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
                    var signature = BitConverter.ToString(hash).Replace("-", "").ToLower();

                    var accountUrl = $"/api/v3/account?{queryString}&signature={signature}";
                    var response = await http.GetAsync(accountUrl);
                    var content = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        _systemLogs?.Append($"✓ {name} SUCCESS", LogLevel.Success);
                        var json = JsonDocument.Parse(content);
                        if (json.RootElement.TryGetProperty("balances", out var balances))
                        {
                            decimal totalUsdt = 0;
                            foreach (var bal in balances.EnumerateArray())
                            {
                                var asset = bal.GetProperty("asset").GetString() ?? "";
                                var free = decimal.Parse(bal.GetProperty("free").GetString() ?? "0");
                                if (asset == "USDT") totalUsdt = free;
                            }
                            _systemLogs?.Append($"  USDT: {totalUsdt:F2}", LogLevel.Info);
                        }
                    }
                    else
                    {
                        _systemLogs?.Append($"✗ {name} FAILED: {response.StatusCode}", LogLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    _systemLogs?.Append($"✗ {name} error: {ex.Message}", LogLevel.Error);
                }
            }

            _systemLogs?.Append("\n=== DIAGNOSIS COMPLETE ===", LogLevel.Info);
        }

        public static class BinanceCreds
        {
            public static bool TryRead(out string key, out string secret, out string mode, SystemLogsControl? logger = null)
            {
                key = Environment.GetEnvironmentVariable("TORRA_BINANCE_KEY") ?? "";
                secret = Environment.GetEnvironmentVariable("TORRA_BINANCE_SECRET") ?? "";
                mode = (Environment.GetEnvironmentVariable("TORRA_BINANCE_MODE") ?? "live").ToLowerInvariant();

                logger?.Append($"Binance credentials: {(string.IsNullOrWhiteSpace(key) ? "NOT FOUND" : "LOADED")}", LogLevel.Info);

                return !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(secret);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _scanTimer?.Dispose();
                _accountTimer?.Dispose();
                _tradeTimer?.Dispose();  // ⭐ DISPOSE TRADE TIMER
            }
            base.Dispose(disposing);
        }
    }
}