using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;

namespace AcomMonitor;

public partial class MainWindow : Window
{
    private readonly Config _config = Config.Load();

    private CoreWebView2Environment? _webEnv;
    private CoreWebView2Controller? _webController;
    private CoreWebView2? _webView;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _pageReady;

    // Peak trackers
    private readonly PeakTracker _peakFwd = new();
    private readonly PeakTracker _peakRef = new();
    private readonly PeakTracker _peakInput = new();
    private readonly PeakTracker _peakGain = new();
    private readonly PeakTracker _peakSwr = new();
    private readonly PeakTracker _peakTemp = new();

    public MainWindow()
    {
        InitializeComponent();
        Opened += MainWindow_Opened;
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        await InitAsync();
        _timer.Tick += async (_, __) => await PollAsync();
        _timer.Start();
    }

    private async Task InitAsync()
    {
        try
        {
            // Get the window handle from Avalonia
            var hwnd = IntPtr.Zero;
            if (TryGetPlatformHandle()?.Handle is IntPtr handle)
            {
                hwnd = handle;
            }

            if (hwnd == IntPtr.Zero)
            {
                Console.WriteLine("Could not get window handle");
                return;
            }

            var userDataFolder = Path.Combine(Path.GetTempPath(), "AcomMonitorWebView2");
            _webEnv = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            _webController = await _webEnv.CreateCoreWebView2ControllerAsync(hwnd);
            _webView = _webController.CoreWebView2;

            _webView.NavigationCompleted += (_, e) => _pageReady = e.IsSuccess;
            _webView.Navigate(_config.AmplifierUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebView2 initialization error: {ex.Message}");
        }
    }

    private const string ScrapeScript = @"
(() => {
  const nodes = document.querySelectorAll('[w-val]');
  const out = {};
  nodes.forEach(n => {
    const p = n.getAttribute('w-val'); if (!p) return;
    let t = (n.innerText || '').replace(/\s+/g,' ').trim();
    out[p] = t;
  });
  return JSON.stringify(out);
})();";

    private static string UnwrapWebView2Json(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            raw = raw.Substring(1, raw.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
        return raw;
    }

    private async Task PollAsync()
    {
        if (!_pageReady || _webView is null) return;

        try
        {
            var js = await _webView.ExecuteScriptAsync(ScrapeScript);
            var cleaned = UnwrapWebView2Json(js);
            if (string.IsNullOrWhiteSpace(cleaned)) return;

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(cleaned);
            if (dict is null || dict.Count == 0) return;

            var snap = dict.ToSnapshot();

            // Update UI on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Peak-tracked values
                ValFwd.Text = _peakFwd.Update(snap.Dashboard.FwdPowerW)?.ToString("0") + " W";
                ValRef.Text = _peakRef.Update(snap.Dashboard.RefPowerW)?.ToString("0") + " W";
                ValInputP.Text = _peakInput.Update(snap.Dashboard.InputPowerW)?.ToString("0") + " W";
                ValGain.Text = _peakGain.Update(snap.Dashboard.GainDb)?.ToString("0.0") + " dB";
                ValSWR.Text = _peakSwr.Update(snap.Dashboard.Swr)?.ToString("0.00");
                ValTempC.Text = _peakTemp.Update(snap.Dashboard.TempC)?.ToString("0") + " °C";

                // Other values
                ValDcV.Text = snap.Dashboard.DcVoltageV?.ToString("0.0") + " V";
                ValDcA.Text = snap.Dashboard.DcCurrentA?.ToString("0.0") + " A";
                ValBiasL.Text = snap.Dashboard.BiasLeftV?.ToString("0.00") + " V";
                ValBiasR.Text = snap.Dashboard.BiasRightV?.ToString("0.00") + " V";
                ValDiss.Text = snap.Dashboard.DissipationW?.ToString("0") + " W";
                ValBand.Text = $"{snap.Band.BandLowMhz} – {snap.Band.BandHighMhz}";
                ValMode.Text = snap.Switches.Mode;
                ValATU.Text = snap.Atu.Status;
                ValCAT.Text = snap.Indicators.CatIsActive ? "CAT ON" : "CAT OFF";
                ValRC.Text = snap.Indicators.LastCmdIsRemote ? "RC" : "";

                // Color highlight for SWR
                ValSWR.Foreground = (snap.Dashboard.Swr ?? 1.0) > 2.0
                    ? new SolidColorBrush(Colors.OrangeRed)
                    : new SolidColorBrush(Colors.LimeGreen);
            });

            // Update button states
            await UpdateButtonStates();
        }
        catch { }
    }

    private async Task UpdateButtonStates()
    {
        if (!_pageReady || _webView is null) return;
        try
        {
            var script = @"
                (function() {
                    const standbyBtn = Array.from(document.querySelectorAll('button')).find(b =>
                        b.textContent.trim() === 'OPERATE' || b.textContent.trim() === 'STANDBY'
                    );
                    const bypassBtn = Array.from(document.querySelectorAll('button')).find(b =>
                        b.textContent.includes('BYPASS')
                    );
                    const powerBtn = Array.from(document.querySelectorAll('button')).find(b =>
                        b.textContent.trim() === 'POWER OFF' || b.textContent.trim() === 'POWER ON'
                    );
                    return JSON.stringify({
                        standby: standbyBtn?.textContent.trim() || '',
                        bypass: bypassBtn?.textContent.trim() || '',
                        power: powerBtn?.textContent.trim() || ''
                    });
                })();
            ";
            var result = await _webView.ExecuteScriptAsync(script);
            var cleaned = UnwrapWebView2Json(result);
            var state = JsonSerializer.Deserialize<Dictionary<string, string>>(cleaned);

            if (state != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (state.TryGetValue("standby", out var standbyText) && !string.IsNullOrEmpty(standbyText))
                    {
                        BtnStandby.Content = standbyText;
                    }

                    if (state.TryGetValue("bypass", out var bypassText) && !string.IsNullOrEmpty(bypassText))
                    {
                        BtnBypass.Content = bypassText;
                    }

                    if (state.TryGetValue("power", out var powerText) && !string.IsNullOrEmpty(powerText))
                    {
                        BtnPowerOff.Content = powerText;
                    }
                });
            }
        }
        catch { }
    }

    private async void OnStandbyClick(object? sender, RoutedEventArgs e)
    {
        if (!_pageReady || _webView is null) return;
        try
        {
            var script = @"
                (function() {
                    let btn = Array.from(document.querySelectorAll('button')).find(b =>
                        b.textContent.trim() === 'OPERATE' ||
                        b.textContent.toLowerCase().includes('standby')
                    );
                    if (btn) {
                        btn.click();
                        return 'Clicked: ' + btn.textContent.trim();
                    }
                    return 'not found';
                })();
            ";
            var result = await _webView.ExecuteScriptAsync(script);
            result = UnwrapWebView2Json(result);
            if (result.Contains("not found"))
            {
                await ShowErrorDialog("Standby/Operate button not found");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Standby error: {ex.Message}");
        }
    }

    private async void OnTuneClick(object? sender, RoutedEventArgs e)
    {
        if (!_pageReady || _webView is null) return;
        try
        {
            var script = @"
                (function() {
                    let btn = Array.from(document.querySelectorAll('button')).find(b =>
                        b.textContent.trim() === 'TUNE'
                    );
                    if (btn) {
                        btn.click();
                        return 'Clicked: ' + btn.textContent.trim();
                    }
                    return 'not found';
                })();
            ";
            var result = await _webView.ExecuteScriptAsync(script);
            result = UnwrapWebView2Json(result);
            if (result.Contains("not found"))
            {
                await ShowErrorDialog("Tune button not found");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Tune error: {ex.Message}");
        }
    }

    private async void OnBypassClick(object? sender, RoutedEventArgs e)
    {
        if (!_pageReady || _webView is null) return;
        try
        {
            var script = @"
                (function() {
                    let btn = Array.from(document.querySelectorAll('button')).find(b =>
                        b.textContent.includes('BYPASS')
                    );
                    if (btn) {
                        btn.click();
                        return 'Clicked: ' + btn.textContent.trim();
                    }
                    return 'not found';
                })();
            ";
            var result = await _webView.ExecuteScriptAsync(script);
            result = UnwrapWebView2Json(result);
            if (result.Contains("not found"))
            {
                await ShowErrorDialog("Bypass button not found");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Bypass error: {ex.Message}");
        }
    }

    private async void OnPowerOffClick(object? sender, RoutedEventArgs e)
    {
        if (!_pageReady || _webView is null) return;
        try
        {
            // Check current button text to customize confirmation
            string currentState = BtnPowerOff.Content?.ToString() ?? "";
            if (currentState == "POWER OFF")
            {
                var result = await ShowConfirmDialog("Are you sure you want to power off the amplifier?");
                if (!result) return;
            }

            var script = @"
                (function() {
                    let btn = Array.from(document.querySelectorAll('button')).find(b =>
                        b.textContent.trim() === 'POWER OFF' || b.textContent.trim() === 'POWER ON'
                    );
                    if (btn) {
                        btn.click();
                        // Wait a bit for dialog to appear, then click OK
                        setTimeout(() => {
                            const okBtn = Array.from(document.querySelectorAll('button')).find(b =>
                                b.textContent.trim() === 'OK'
                            );
                            if (okBtn) okBtn.click();
                        }, 100);
                        return 'Clicked: ' + btn.textContent.trim();
                    }
                    return 'not found';
                })();
            ";
            var jsResult = await _webView.ExecuteScriptAsync(script);
            jsResult = UnwrapWebView2Json(jsResult);
            if (jsResult.Contains("not found"))
            {
                await ShowErrorDialog("Power button not found");
            }
            else
            {
                // Wait a bit for the action to complete, then update button states
                await Task.Delay(500);
                await UpdateButtonStates();
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Power error: {ex.Message}");
        }
    }

    private async Task ShowErrorDialog(string message)
    {
        var msgBox = new Window
        {
            Title = "Error",
            Width = 400,
            Height = 150,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, Margin = new Avalonia.Thickness(0, 0, 0, 20) },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center }
                }
            }
        };

        var okButton = (msgBox.Content as StackPanel)!.Children[1] as Button;
        okButton!.Click += (_, __) => msgBox.Close();

        await msgBox.ShowDialog(this);
    }

    private async Task<bool> ShowConfirmDialog(string message)
    {
        var result = false;
        var msgBox = new Window
        {
            Title = "Confirm",
            Width = 400,
            Height = 150,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, Margin = new Avalonia.Thickness(0, 0, 0, 20) },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Children =
                        {
                            new Button { Content = "Yes", Width = 80, Margin = new Avalonia.Thickness(5) },
                            new Button { Content = "No", Width = 80, Margin = new Avalonia.Thickness(5) }
                        }
                    }
                }
            }
        };

        var buttonPanel = (msgBox.Content as StackPanel)!.Children[1] as StackPanel;
        var yesButton = buttonPanel!.Children[0] as Button;
        var noButton = buttonPanel.Children[1] as Button;

        yesButton!.Click += (_, __) => { result = true; msgBox.Close(); };
        noButton!.Click += (_, __) => { result = false; msgBox.Close(); };

        await msgBox.ShowDialog(this);
        return result;
    }
}

/* ========== PEAK TRACKER ========== */
public sealed class PeakTracker
{
    public double? Current { get; private set; }
    private DateTime _lastUpdate = DateTime.MinValue;
    private readonly TimeSpan _hold;

    public PeakTracker(double seconds = 3.0) => _hold = TimeSpan.FromSeconds(seconds);

    public double? Update(double? value)
    {
        if (value == null) return Current;

        if (Current == null || value > Current)
        {
            Current = value;
            _lastUpdate = DateTime.UtcNow;
        }
        else if (DateTime.UtcNow - _lastUpdate > _hold)
        {
            Current = value;
            _lastUpdate = DateTime.UtcNow;
        }
        return Current;
    }
}

/* ========== MODEL + MAPPER (short) ========== */
public sealed class AmpSnapshot
{
    public BandInfo Band { get; init; } = new();
    public Indicators Indicators { get; init; } = new();
    public Switches Switches { get; init; } = new();
    public Dashboard Dashboard { get; init; } = new();
    public AtuInfo Atu { get; init; } = new();
}

public sealed class BandInfo { public string BandLowMhz = ""; public string BandHighMhz = ""; }
public sealed class Indicators { public bool CatIsActive; public bool LastCmdIsRemote; }
public sealed class Switches { public string Mode = ""; }
public sealed class Dashboard
{
    public double? FwdPowerW, RefPowerW, InputPowerW, DissipationW, Swr, GainDb, BiasLeftV, BiasRightV, DcVoltageV, DcCurrentA, TempC;
    public string TempRel = "";
}
public sealed class AtuInfo { public string Status = ""; public double? AtuaSwr; public double? AtuaTempC; }

public static class AmpSnapshotMapper
{
    private static string Get(this IDictionary<string, string> d, string k, string def = "") =>
        d.TryGetValue(k, out var v) ? v : def;

    private static double? Num(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = Regex.Match(s.Replace(',', '.'), @"-?\d+(\.\d+)?");
        return m.Success ? double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    public static AmpSnapshot ToSnapshot(this IDictionary<string, string> d) => new()
    {
        Band = new BandInfo
        {
            BandLowMhz = d.Get("$amp/controls/dashboard/band/band_low_border_mhz"),
            BandHighMhz = d.Get("$amp/controls/dashboard/band/band_high_border_mhz")
        },
        Indicators = new Indicators
        {
            CatIsActive = d.Get("$amp/controls/dashboard/indicators/cat_is_active").Contains("ON", StringComparison.OrdinalIgnoreCase),
            LastCmdIsRemote = d.Get("$amp/controls/dashboard/indicators/last_cmd_is_remote").Contains("RC", StringComparison.OrdinalIgnoreCase)
        },
        Switches = new Switches { Mode = d.Get("$amp/controls/dashboard/switches/mode") },
        Dashboard = new Dashboard
        {
            FwdPowerW = Num(d.Get("$amp/controls/dashboard/values/forward_power")),
            RefPowerW = Num(d.Get("$amp/controls/dashboard/values/reflected_power")),
            InputPowerW = Num(d.Get("$amp/controls/dashboard/values/input_power")),
            DissipationW = Num(d.Get("$amp/controls/dashboard/values/dissipated_power")),
            Swr = Num(d.Get("$amp/controls/dashboard/values/swr")),
            GainDb = Num(d.Get("$amp/controls/dashboard/values/power_gain")),
            BiasLeftV = Num(d.Get("$amp/controls/dashboard/values/bias/bias_1a")),
            BiasRightV = Num(d.Get("$amp/controls/dashboard/values/bias/bias_1b")),
            DcVoltageV = Num(d.Get("$amp/controls/dashboard/values/hv/hv1")),
            DcCurrentA = Num(d.Get("$amp/controls/dashboard/values/id/id1")),
            TempC = Num(d.Get("$amp/controls/dashboard/values/temperature_c")),
            TempRel = d.Get("$amp/controls/dashboard/values/temperature_rel")
        },
        Atu = new AtuInfo
        {
            Status = d.Get("$amp/controls/atu/status"),
            AtuaSwr = Num(d.Get("$amp/controls/atu/measure/values/swr")),
            AtuaTempC = Num(d.Get("$amp/controls/atu/measure/values/temperature"))
        }
    };
}
