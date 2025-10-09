using System;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using torra_watch.Core;
using torra_watch.UI;
using AccountPanel = torra_watch.UI.Controler.AccountPanel;
using HeaderBar = torra_watch.UI.Controler.HeaderBar;
using LogPanel = torra_watch.UI.Controler.LogPanel;
using TickersList = torra_watch.UI.Controler.TickersList;

namespace torra_watch
{
    public partial class MainForm : Form
    {
        private readonly IExchange _ex;
        private readonly RankingService _ranking;
        private readonly DecisionEngine _engine;
        private readonly Trader _trader;
        private readonly StrategyConfig _cfg;

        // --- Figma-like shell ---
        private HeaderBar _header;
        private TickersList _tickers;
        private LogPanel _logPanel;
        private AccountPanel _acctPanel;
        private TableLayoutPanel _root;

        // settings controls (same instances you had)
        private readonly GroupBox _gbSettings = new() { Text = "Settings", Dock = DockStyle.Fill };
        private readonly NumericUpDown _numUniverse = new() { Minimum = 50, Maximum = 500, Increment = 10, Width = 90 };
        private readonly NumericUpDown _numDrop3h = new() { Minimum = -50, Maximum = 0, DecimalPlaces = 1, Increment = 0.1M, Width = 90 };
        private readonly NumericUpDown _numTP = new() { Minimum = 0.1M, Maximum = 10, DecimalPlaces = 1, Increment = 0.1M, Width = 90 };
        private readonly NumericUpDown _numSL = new() { Minimum = 0.1M, Maximum = 10, DecimalPlaces = 1, Increment = 0.1M, Width = 90 };
        private readonly NumericUpDown _numTimeStop = new() { Minimum = 0.5M, Maximum = 24, DecimalPlaces = 1, Increment = 0.5M, Width = 90 };
        private readonly Button _btnApplySettings = new() { Text = "Apply", Height = 30, Width = 90, Dock = DockStyle.Right };

        // debug viewer (shown in Account panel on demand)
        private readonly RichTextBox _acctDebug = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            DetectUrls = false,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Visible = false,
            BackColor = UiTheme.Bg3,
            ForeColor = UiTheme.Text,
            BorderStyle = BorderStyle.None
        };

        // loop state
        private CancellationTokenSource? _cts;
        private DateTime? _nextCheckUtc;

        public MainForm(IExchange ex, RankingService ranking, DecisionEngine engine, Trader trader, StrategyConfig cfg)
        {
            _ex = ex; _ranking = ranking; _engine = engine; _trader = trader; _cfg = cfg;

            InitializeComponent();
            Text = "Torra Watch — UI";
            WindowState = FormWindowState.Maximized;

            BuildShell();
            WireHeaderEvents();

            // settings UI into header settings slot
            AddSettingsToHeader();

            // grid columns
            _tickers.ConfigureColumns(_cfg);

            // initial values
            _numUniverse.Value = _cfg.UniverseSize;
            _numDrop3h.Value = (decimal)(_cfg.MinDrop3hPct * 100m);
            _numTP.Value = (decimal)(_cfg.TakeProfitPct * 100m);
            _numSL.Value = (decimal)(_cfg.StopLossPct * 100m);
            _numTimeStop.Value = (decimal)_cfg.TimeStopHours;

            Shown += async (_, __) =>
            {
                try
                {
                    AppendLog("Init refresh…");
                    await RefreshTickersAsync();
                    await RefreshEquityAsync();
                    await RefreshEnvStatusAsync();
                    await RefreshAccountSummaryAsync(); // light summary on the account card
                    AppendLog("Init done.");
                }
                catch (Exception ex)
                {
                    AppendLog("Init error: " + ex.Message);
                    MessageBox.Show(ex.ToString(), "Init Error");
                }
            };

            EnableDoubleBuffer(_tickers.Grid);
        }

        // ========================= LAYOUT =========================

        private void BuildShell()
        {
            _header = new HeaderBar();
            _tickers = new TickersList();
            _logPanel = new LogPanel();
            _acctPanel = new AccountPanel();

            // ==== Put your Figma percentages here ====
            var leftPct = 28f;    // tickers
            var middlePct = 44f;  // logs (and future chart)
            var rightPct = 28f;   // account + settings
            var gapPct = 1f;
            // ========================================

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 2,
                Padding = new Padding(16)
            };
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, leftPct));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, gapPct));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, middlePct));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, gapPct));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, rightPct));

            Controls.Clear();
            Controls.Add(_root);

            // header
            _root.Controls.Add(_header, 0, 0);
            _root.SetColumnSpan(_header, 5);

            // main content
            _root.Controls.Add(_tickers, 0, 1);
            _root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 1, 1);
            _root.Controls.Add(_logPanel, 2, 1);
            _root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 3, 1);

            // right column: a vertical split (Account summary on top, Settings below)
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // Account card gets a small summary area + hidden debug box overlay
            var acctWrap = new Panel { Dock = DockStyle.Fill };
            acctWrap.Controls.Add(_acctPanel);
            acctWrap.Controls.Add(_acctDebug); // toggled visible by Account/Orders buttons
            right.Controls.Add(acctWrap, 0, 0);

            // Settings card already styled via HeaderBar slot (we place a “proxy” below for symmetry)
            var settingsProxy = new CardPanel { Dock = DockStyle.Fill };
            settingsProxy.Controls.Add(new Label
            {
                Text = "Settings → in header",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.TextDim
            });
            right.Controls.Add(settingsProxy, 0, 1);

            _root.Controls.Add(right, 4, 1);
        }

        private void AddSettingsToHeader()
        {
            var sp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
            sp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            sp.Controls.Add(new Label { Text = "Universe", AutoSize = true }, 0, 0); sp.Controls.Add(_numUniverse, 1, 0);
            sp.Controls.Add(new Label { Text = "Min drop 3h (%)", AutoSize = true }, 0, 1); sp.Controls.Add(_numDrop3h, 1, 1);
            sp.Controls.Add(new Label { Text = "TP (%)", AutoSize = true }, 0, 2); sp.Controls.Add(_numTP, 1, 2);
            sp.Controls.Add(new Label { Text = "SL (%)", AutoSize = true }, 0, 3); sp.Controls.Add(_numSL, 1, 3);
            sp.Controls.Add(new Label { Text = "Time-stop (h)", AutoSize = true }, 0, 4); sp.Controls.Add(_numTimeStop, 1, 4);
            sp.Controls.Add(_btnApplySettings, 1, 5);

            _header.SettingsBox.Controls.Clear();
            _header.SettingsBox.Controls.Add(sp);
        }

        // ========================= THEME HELPERS =========================

        private static void EnableDoubleBuffer(DataGridView grid)
        {
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.SetProperty,
                    null, grid, new object[] { true });
            }
            catch { }
        }

        // ========================= REFRESHERS =========================

        private async System.Threading.Tasks.Task RefreshTickersAsync()
        {
            var data = await _ranking.BuildAsync(_cfg.UniverseSize);
            _tickers.Bind(data);
        }

        private async System.Threading.Tasks.Task RefreshAccountSummaryAsync()
        {
            try
            {
                var eq = await _ex.GetEquityAsync();
                // show a minimal summary in the Account card’s header text
                // (If you want lines like “Available / In Orders”, add labels in AccountPanel)
                _acctPanel.SetText($"// Account summary\nEquity (quote): {eq:0,0.00}");
                _acctDebug.Visible = false; // keep debug hidden until user clicks Account/Orders
            }
            catch (Exception ex)
            {
                _acctPanel.SetText($"Account: n/a ({ex.Message})");
            }
        }

        private async System.Threading.Tasks.Task RefreshEquityAsync()
        {
            try
            {
                var eq = await _ex.GetEquityAsync();
                _header.Equity.Font = UiTheme.H1;
                _header.Equity.ForeColor = UiTheme.Accent;
                _header.Equity.Text = $"Equity: {eq:0,0.00}";
            }
            catch
            {
                _header.Equity.Text = "Equity: n/a";
                _header.Equity.ForeColor = UiTheme.TextDim;
                _header.Equity.Font = UiTheme.H2;
            }
        }

        private async System.Threading.Tasks.Task RefreshEnvStatusAsync()
        {
            try
            {
                if (_ex is Exchange.BinanceHttpExchange http)
                {
                    var snap = await http.GetEnvSnapshotAsync();
                    var balances = snap.balances.Count == 0
                        ? "balances: n/a"
                        : string.Join(", ", snap.balances.Select(b => $"{b.Asset}:{(b.Free + b.Locked):0.####}"));
                    _header.Env.Text = $"Env: {snap.env} | Pub: {snap.publicHost} | Priv: {snap.privateHost} | Keys: {(snap.keysLoaded ? "yes" : "no")} | {balances}";
                }
                else
                {
                    _header.Env.Text = "Env: PAPER";
                }
            }
            catch (Exception ex)
            {
                _header.Env.Text = $"Env: error — {ex.Message}";
            }
        }

        // ========================= EVENTS / ACTIONS =========================

        private void WireHeaderEvents()
        {
            _btnApplySettings.Click += (_, __) => ApplySettings();

            _header.BtnStart.Click += (_, __) => { AppendLog("Start clicked"); StartLoop(); };
            _header.BtnStop.Click += (_, __) => { AppendLog("Stop clicked"); StopLoop(); };
            _header.BtnCheck.Click += async (_, __) => { AppendLog("Check clicked"); await CheckOnceAsync(); };
            _header.BtnPanic.Click += async (_, __) => { AppendLog("Panic clicked"); await PanicCloseAsync(); };
            _header.BtnAcct.Click += async (_, __) => { AppendLog("Account clicked"); await ShowAccountDebugAsync(); };
            _header.BtnOrders.Click += async (_, __) => { AppendLog("Orders clicked"); await ShowOrdersForSelectionAsync(); };
        }

        private void ApplySettings()
        {
            _cfg.UniverseSize = (int)_numUniverse.Value;
            _cfg.MinDrop3hPct = _numDrop3h.Value / 100m;
            if (_cfg.MinDrop3hPct > 0m) _cfg.MinDrop3hPct = -_cfg.MinDrop3hPct;
            _cfg.TakeProfitPct = _numTP.Value / 100m;
            _cfg.StopLossPct = _numSL.Value / 100m;
            _cfg.TimeStopHours = (double)_numTimeStop.Value;

            AppendLog($"Settings applied: N={_cfg.UniverseSize}, 3h≤{_cfg.MinDrop3hPct:P1}, TP={_cfg.TakeProfitPct:P1}, SL={_cfg.StopLossPct:P1}, TS={_cfg.TimeStopHours:0.0}h");
            _ = RefreshTickersAsync();
            _ = RefreshEnvStatusAsync();
        }

        private void AppendLog(string msg) =>
            _logPanel.Append($"[{DateTime.Now:HH:mm:ss}] {msg}");

        private async System.Threading.Tasks.Task ShowAccountDebugAsync()
        {
            try
            {
                if (_ex is Exchange.BinanceHttpExchange http)
                {
                    var json = await http.DebugAccountRawAsync();
                    _acctDebug.Text = PrettyJson(json);
                    _acctDebug.Visible = !_acctDebug.Visible; // toggle
                }
            }
            catch (Exception ex)
            {
                _acctDebug.Text = $"Account error: {ex.Message}";
                _acctDebug.Visible = true;
            }
        }

        private async System.Threading.Tasks.Task ShowOrdersForSelectionAsync()
        {
            try
            {
                var symbol = "BTCUSDT";
                if (_tickers.Grid.CurrentRow?.DataBoundItem is RankingRow rr && !string.IsNullOrWhiteSpace(rr.Symbol))
                    symbol = rr.Symbol;

                if (_ex is Exchange.BinanceHttpExchange http)
                {
                    var open = await http.DebugOpenOrdersRawAsync(symbol);
                    var recent = await http.DebugAllOrdersRawAsync(symbol);
                    _acctDebug.Text = $"// Open Orders ({symbol})\n{PrettyJson(open)}\n\n// All Orders ({symbol})\n{PrettyJson(recent)}";
                    _acctDebug.Visible = true;
                }
            }
            catch (Exception ex)
            {
                _acctDebug.Text = $"Orders error: {ex.Message}";
                _acctDebug.Visible = true;
            }
        }

        // ========================= CHECK / LOOP =========================

        private async System.Threading.Tasks.Task CheckOnceAsync()
        {
            _header.State.Text = "State: Checking…";
            await RefreshTickersAsync();

            var decision = await _engine.DecideAsync();
            if (decision.kind == DecisionKind.CandidateFound && !string.IsNullOrWhiteSpace(decision.symbol))
            {
                _header.State.Text = $"State: Candidate → {decision.symbol} ({(decision.ret3h.GetValueOrDefault() * 100m):0.00}% 3h)";
                _header.Next.Text = "Next check: asap";
                AppendLog($"Candidate found: {decision.symbol} ({decision.ret3h.GetValueOrDefault():P2})");
            }
            else
            {
                _nextCheckUtc = decision.nextCheckUtc;
                _header.State.Text = "State: Cooldown";
                UpdateNextLabel();
                AppendLog(decision.note);
            }
        }

        private void StartLoop()
        {
            if (_cts != null) return;
            _cts = new System.Threading.CancellationTokenSource();
            _header.BtnStart.Enabled = false; _header.BtnStop.Enabled = true;
            AppendLog("Loop started.");
            _ = TradeLoopAsync(_cts.Token);
        }

        private void StopLoop()
        {
            _cts?.Cancel();
            _cts = null;
            _header.BtnStart.Enabled = true; _header.BtnStop.Enabled = false;
            _header.State.Text = "State: Idle"; _header.Next.Text = "Next check: —";
            AppendLog("Loop stopped.");
        }

        private async System.Threading.Tasks.Task PanicCloseAsync()
        {
            AppendLog("Panic Close requested — ensure adapter closes open position if any.");
        }

        private async System.Threading.Tasks.Task TradeLoopAsync(System.Threading.CancellationToken ct)
        {
            bool liveLike = _ex is Exchange.BinanceHttpExchange;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_nextCheckUtc is DateTime next && DateTime.UtcNow < next)
                    {
                        UpdateNextLabel();
                        await System.Threading.Tasks.Task.Delay(1000, ct);
                        continue;
                    }

                    AppendLog("Loop tick: refresh tickers/equity…");
                    await RefreshTickersAsync();
                    await RefreshEquityAsync();
                    await RefreshAccountSummaryAsync();

                    AppendLog("Loop tick: deciding…");
                    var decision = await _engine.DecideAsync(ct);
                    var sym = string.IsNullOrWhiteSpace(decision.symbol) ? "-" : decision.symbol;
                    var ret = decision.ret3h.HasValue ? decision.ret3h.Value.ToString("P2") : "";
                    AppendLog($"Decision: {decision.kind} {sym} {ret}");

                    if (decision.kind != DecisionKind.CandidateFound || string.IsNullOrWhiteSpace(decision.symbol))
                    {
                        _nextCheckUtc = decision.nextCheckUtc;
                        _header.State.Text = $"State: {decision.note}";
                        AppendLog(decision.note);
                        UpdateNextLabel();
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(3), ct);
                        continue;
                    }

                    var (entered, symbol, note) = await _trader.TryEnterAsync(ct);

                    if (!entered)
                    {
                        _header.State.Text = $"State: No entry — {note}";
                        AppendLog(_header.State.Text);
                        _nextCheckUtc ??= decision.nextCheckUtc;
                        UpdateNextLabel();
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(3), ct);
                        continue;
                    }

                    _header.State.Text = $"State: Entered — {note}";
                    AppendLog(_header.State.Text);

                    var entryMid = ExtractEntryPrice(note);
                    var qty = ExtractQty(note);

                    if (!liveLike)
                    {
                        var outcome = await _trader.WaitAndSettleAsync(symbol!, DateTime.UtcNow, entryMid, qty, ct);
                        if (outcome is not null)
                        {
                            AppendLog($"Exit {outcome.Symbol} — {outcome.Reason} @ {outcome.ExitPrice:0.########} | PnL {outcome.PnL:0.00}");
                            _header.State.Text = $"Exit: {outcome.Symbol} {outcome.Reason} | PnL {outcome.PnL:0.00}";
                            await RefreshEquityAsync();
                            await RefreshAccountSummaryAsync();
                        }
                    }
                    else
                    {
                        AppendLog("Live/demo: OCO managing exit; pausing before next scan…");
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(20), ct);
                    }

                    _nextCheckUtc = null;
                    _header.Next.Text = "Next check: asap";
                    await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
                catch (System.Threading.Tasks.TaskCanceledException) { }
                catch (Exception ex)
                {
                    AppendLog("Loop error: " + ex.Message);
                    await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
            }
        }

        // ========================= UTIL =========================

        private void UpdateNextLabel()
        {
            if (_nextCheckUtc is null) { _header.Next.Text = "Next check: asap"; return; }
            var rem = _nextCheckUtc.Value - DateTime.UtcNow;
            if (rem < TimeSpan.Zero) rem = TimeSpan.Zero;
            _header.Next.Text = $"Next check in: {rem:hh\\:mm\\:ss} (UTC {_nextCheckUtc:HH:mm:ss})";
        }

        private static string PrettyJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch { return json; }
        }

        private static decimal ExtractEntryPrice(string note)
        {
            var atIdx = note.IndexOf('@');
            if (atIdx < 0) return 0m;
            var comma = note.IndexOf(',', atIdx + 1);
            var span = note[(atIdx + 1)..(comma > atIdx ? comma : note.Length)].Trim();
            return decimal.TryParse(span, out var d) ? d : 0m;
        }

        private static decimal ExtractQty(string note)
        {
            const string k = "qty";
            var i = note.IndexOf(k, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return 0m;
            var tail = note[(i + k.Length)..];
            var end = tail.IndexOf(',');
            var s = (end >= 0 ? tail[..end] : tail).Trim();
            return decimal.TryParse(s, out var d) ? d : 0m;
        }
    }
}
