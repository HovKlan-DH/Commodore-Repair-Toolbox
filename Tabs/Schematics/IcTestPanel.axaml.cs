using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Handlers.DataHandling;   // Logger
using Handlers.IcTesting;

namespace CRT
{
    // ###########################################################################################
    // Runs one IC test (logic / PLA / ROM) and shows an honest result. Offers a Quick/Standard/
    // Full depth selector for parts that ship multiple sets (e.g. the PLA). Drives the real
    // MiniproProcessRunner, or the MockMiniproRunner in demo mode (no hardware). All device work
    // is async + cancellable, with a live elapsed timer so a long run never looks frozen.
    //
    // Embedded overlay (not a Window) inside ComponentInfoWindow's right panel — the host shows/
    // hides it and re-uses the same instance across multiple "Test this IC" clicks, so Load()
    // must fully reset state (including cancelling any still-running previous test).
    // ###########################################################################################
    public partial class IcTestPanel : UserControl
    {
        private IcTestEntry? _entry;
        private string _boardLabel = string.Empty;
        private List<IcTestMode> _modes = new();
        private CancellationTokenSource? _cts;
        private DispatcherTimer? _elapsedTimer;
        private readonly Stopwatch _elapsed = new();

        // Raised when the user clicks Close — the host is responsible for hiding this panel
        // and restoring whatever it was overlaying.
        public event Action? CloseRequested;

        public IcTestPanel()
        {
            InitializeComponent();
        }

        public void Load(IcTestEntry entry, string boardLabel)
        {
            this._cts?.Cancel();   // a previous test may still be running if the panel is being re-used
            this._cts = null;

            this._entry = entry;
            this._boardLabel = boardLabel;

            this.ResultBorder.IsVisible = false;
            this.LogExpander.IsExpanded = false;
            this.LogBox.Text = string.Empty;

            this._modes = entry.Modes ?? new List<IcTestMode>();
            if (this._modes.Count > 0)
            {
                this.ModeCombo.ItemsSource = this._modes.Select(m => m.Label).ToList();
                this.ModeCombo.SelectedIndex = 0;   // Quick first — fast feedback
                this.ModePanel.IsVisible = true;
                this.SingleModePanel.IsVisible = false;
            }
            else
            {
                this.ModePanel.IsVisible = false;

                // Single-depth parts still have a fixed vector count worth showing —
                // just as a disabled choice instead of a real dropdown.
                if (entry.IsTestable)
                {
                    int count = entry.VectorCount > 0 ? entry.VectorCount : (entry.Vectors?.Count ?? 0);
                    this.SingleModeCombo.ItemsSource = new[] { $"{count} vectors" };
                    this.SingleModeCombo.SelectedIndex = 0;
                    this.SingleModePanel.IsVisible = true;
                }
                else
                {
                    this.SingleModePanel.IsVisible = false;
                }
            }

            this.SetRunning(false);
            this.RunButton.IsEnabled = entry.IsTestable;

            this.DemoModeCheck.IsVisible = UserSettings.EnableMiniproExperimentalDemoMode;
            this.DemoModeCheck.IsChecked = this.DemoModeCheck.IsVisible && !MiniproPresent();

            this.UpdateCoverageText();
        }

        private IcTestMode? SelectedMode =>
            this._modes.Count > 0 && this.ModeCombo.SelectedIndex >= 0
                ? this._modes[this.ModeCombo.SelectedIndex]
                : null;

        private void OnModeChanged(object? sender, SelectionChangedEventArgs e) => this.UpdateCoverageText();

        private void UpdateCoverageText()
        {
            if (this._entry is null) return;
            if (this._entry.IsFunctionalOnly)
            {
                this.CoverageText.Text =
                    "Functional-only part: a vector test is a functional check, not exhaustive.";
                return;
            }
            if (!this._entry.IsTestable)
            {
                this.CoverageText.Text =
                    "This part is not vector-testable — no automated test is available.";
                return;
            }
            this.CoverageText.Text =
                "A pass means the truth table held, but this is static test only and no timing tested. A fail is definitive and can be trusted.";
        }

        private static bool MiniproPresent()
        {
            try
            {
                var bin = new MiniproProcessRunner().ResolveBinary();
                if (string.IsNullOrEmpty(bin)) return false;
                // ResolveBinary() now returns an absolute path whenever it found the binary
                // (bundled, or via the PATH/common-install-dir probe); a bare name back here
                // means it wasn't found anywhere reasonable.
                return System.IO.Path.IsPathRooted(bin) && System.IO.File.Exists(bin);
            }
            catch
            {
                return false;
            }
        }

        private async void OnRun(object? sender, RoutedEventArgs e)
        {
            if (this._entry is null) return;
            var mode = this.SelectedMode;

            this.SetRunning(true);
            this.ResultBorder.IsVisible = false;
            this.LogExpander.IsExpanded = true;
            this.LogBox.Text = string.Empty;
            this._cts = new CancellationTokenSource();
            this.StartElapsed(mode);

            IMiniproRunner runner = this.DemoModeCheck.IsChecked == true
                ? new MockMiniproRunner { Scenario = MockScenario.GoodChip }
                : new MiniproProcessRunner();
            var service = new IcTestService(runner);
            var progress = new Progress<string>(this.AppendLog);

            Logger.Info($"IC test run: [{this._boardLabel}] [{this._entry.Id}] " +
                        $"mode=[{mode?.Name ?? "(single)"}] demo=[{this.DemoModeCheck.IsChecked == true}]");

            IcTestResult result;
            try
            {
                result = await service.RunAsync(this._entry, mode, progress, this._cts.Token);
            }
            catch (OperationCanceledException)
            {
                this.AppendLog("— cancelled —");
                Logger.Info($"IC test cancelled: [{this._boardLabel}] [{this._entry.Id}]");
                this.SetRunning(false);
                return;
            }
            catch (Exception ex)
            {
                result = IcTestResult.Connectionless(MiniproConnectionState.Unknown, ex.Message);
            }

            this.RenderResult(result);
            Logger.Info($"IC test result: [{this._boardLabel}] [{this._entry.Id}] -> {result.Outcome} ({result.Headline})");
            this.SetRunning(false);
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => this._cts?.Cancel();

        private void OnClose(object? sender, RoutedEventArgs e)
        {
            this._cts?.Cancel();
            this.CloseRequested?.Invoke();
        }

        private void SetRunning(bool running)
        {
            this.RunButton.IsEnabled = !running && (this._entry?.IsTestable ?? false);
            this.CancelButton.IsEnabled = running;
            this.ModeCombo.IsEnabled = !running;
            this.DemoModeCheck.IsEnabled = !running;
            if (running) return;
            this._elapsedTimer?.Stop();
            this._elapsedTimer = null;
            this._elapsed.Stop();
            this.ElapsedText.Text = string.Empty;
        }

        private void StartElapsed(IcTestMode? mode)
        {
            this._elapsed.Restart();
            bool slow = (mode?.VectorCount ?? this._entry?.VectorCount ?? 0) > 1000;
            this.ElapsedText.Text = slow ? "Testing… (the full set can take ~2 min)" : "Testing…";
            this._elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            this._elapsedTimer.Tick += (_, _) =>
                this.ElapsedText.Text = $"Testing… {this._elapsed.Elapsed.Minutes}:{this._elapsed.Elapsed.Seconds:00}";
            this._elapsedTimer.Start();
        }

        private void AppendLog(string line) =>
            this.LogBox.Text += ((this.LogBox.Text?.Length ?? 0) > 0 ? "\n" : string.Empty)
                + MiniproOutputParser.StripAnsi(line);

        private void RenderResult(IcTestResult r)
        {
            this.ResultBorder.IsVisible = true;
            (string label, Color colour) = r.Outcome switch
            {
                TestOutcome.Pass => ("PASS", Color.FromRgb(0x2e, 0x7d, 0x32)),
                TestOutcome.Fail => ("FAIL", Color.FromRgb(0xc6, 0x28, 0x28)),
                TestOutcome.Unsupported => ("NOT TESTED", Color.FromRgb(0x61, 0x61, 0x61)),
                _ => ("ERROR", Color.FromRgb(0xc6, 0x28, 0x28)),
            };
            this.OutcomeText.Text = label;
            this.OutcomeText.Foreground = new SolidColorBrush(colour);
            this.ResultBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, colour.R, colour.G, colour.B));

            // OutcomeText already shows PASS/FAIL big — don't repeat it in the headline.
            var headline = r.Headline;
            foreach (var p in new[] { "PASS — ", "FAIL — " })
                if (headline.StartsWith(p, StringComparison.Ordinal)) { headline = headline[p.Length..]; break; }
            this.HeadlineText.Text = headline;

            // Only the actionable extras here — coverage is already stated above the button.
            var detail = new StringBuilder();
            if (r.FailingPins.Count > 0) detail.Append($"Failing pin: {string.Join(", ", r.FailingPins)}. ");
            this.DetailText.Text = detail.ToString();
            this.DetailText.IsVisible = detail.Length > 0;

            // Show the complete minipro output (stdout + stderr). Errors land on
            // stderr, which the live stdout stream above never surfaces — so on a
            // failure the box would otherwise look empty.
            if (!string.IsNullOrEmpty(r.RawOutput))
                this.LogBox.Text = MiniproOutputParser.AlignVectorTableHeader(MiniproOutputParser.StripAnsi(r.RawOutput));
        }
    }
}
