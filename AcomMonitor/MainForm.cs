using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.WinForms;
using Timer = System.Windows.Forms.Timer;

namespace AcomMonitor;

public sealed class MainForm : Form
{
    private const string AmpUrl = "http://192.168.1.68/"; // your amp IP

    private readonly WebView2 web = new();
    private readonly Timer timer = new() { Interval = 250 };
    private bool pageReady;

    private readonly TableLayoutPanel dash = new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        AutoSize = true,
        Padding = new Padding(20),
        BackColor = Color.Black
    };

    // Dashboard labels
    private readonly Label lblFwd = L("FWD"); private readonly Label valFwd = LV();
    private readonly Label lblRef = L("REF"); private readonly Label valRef = LV();
    private readonly Label lblSWR = L("SWR"); private readonly Label valSWR = LV();
    private readonly Label lblGain = L("Gain"); private readonly Label valGain = LV();
    private readonly Label lblInputP = L("Input P"); private readonly Label valInputP = LV();

    private readonly Label lblDcV = L("DC V"); private readonly Label valDcV = LV();
    private readonly Label lblDcA = L("DC A"); private readonly Label valDcA = LV();
    private readonly Label lblBiasL = L("Bias L"); private readonly Label valBiasL = LV();
    private readonly Label lblBiasR = L("Bias R"); private readonly Label valBiasR = LV();

    private readonly Label lblTempC = L("Temp C"); private readonly Label valTempC = LV();
    private readonly Label lblTempRel = L("Temp Rel"); private readonly Label valTempRel = LV();
    private readonly Label lblDiss = L("Dissip"); private readonly Label valDiss = LV();

    private readonly Label lblBand = L("Band"); private readonly Label valBand = LV();
    private readonly Label lblMode = L("Mode"); private readonly Label valMode = LV();
    private readonly Label lblATU = L("ATU"); private readonly Label valATU = LV();
    private readonly Label lblCAT = L("CAT"); private readonly Label valCAT = LV();
    private readonly Label lblRC = L("RC"); private readonly Label valRC = LV();

    // Peak trackers
    private readonly PeakTracker peakFwd = new();
    private readonly PeakTracker peakRef = new();
    private readonly PeakTracker peakInput = new();
    private readonly PeakTracker peakGain = new();
    private readonly PeakTracker peakSWR = new();
    private readonly PeakTracker peakTemp = new();

    public MainForm()
    {
        Text = "ACOM 1400S Live Monitor";
        Width = 1000; Height = 700;
        BackColor = Color.Black;
        ForeColor = Color.White;

        dash.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        dash.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

        void Row(Control a, Control b)
        {
            int r = dash.RowCount++;
            dash.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            dash.Controls.Add(a, 0, r);
            dash.Controls.Add(b, 1, r);
        }

        Row(lblFwd, valFwd);
        Row(lblRef, valRef);
        Row(lblSWR, valSWR);
        Row(lblGain, valGain);
        Row(lblInputP, valInputP);
        Row(lblDcV, valDcV);
        Row(lblDcA, valDcA);
        Row(lblBiasL, valBiasL);
        Row(lblBiasR, valBiasR);
        Row(lblTempC, valTempC);
        Row(lblTempRel, valTempRel);
        Row(lblDiss, valDiss);
        Row(new Label { Height = 15 }, new Label());
        Row(lblBand, valBand);
        Row(lblMode, valMode);
        Row(lblATU, valATU);
        Row(lblCAT, valCAT);
        Row(lblRC, valRC);

        Controls.Add(dash);

        Shown += async (_, __) => await InitAsync();
        timer.Tick += async (_, __) => await PollAsync();
    }

    private async System.Threading.Tasks.Task InitAsync()
    {
        await web.EnsureCoreWebView2Async();
        web.CoreWebView2.NavigationCompleted += (_, e) => pageReady = e.IsSuccess;
        web.Source = new Uri(AmpUrl);
        timer.Start();
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

    private async System.Threading.Tasks.Task PollAsync()
    {
        if (!pageReady || web.CoreWebView2 is null) return;

        try
        {
            var js = await web.CoreWebView2.ExecuteScriptAsync(ScrapeScript);
            var cleaned = UnwrapWebView2Json(js);
            if (string.IsNullOrWhiteSpace(cleaned)) return;

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(cleaned);
            if (dict is null || dict.Count == 0) return;

            var snap = dict.ToSnapshot();

            // Peak-tracked values
            valFwd.Text    = peakFwd.Update(snap.Dashboard.FwdPowerW)?.ToString("0") + " W";
            valRef.Text    = peakRef.Update(snap.Dashboard.RefPowerW)?.ToString("0") + " W";
            valInputP.Text = peakInput.Update(snap.Dashboard.InputPowerW)?.ToString("0") + " W";
            valGain.Text   = peakGain.Update(snap.Dashboard.GainDb)?.ToString("0.0") + " dB";
            valSWR.Text    = peakSWR.Update(snap.Dashboard.Swr)?.ToString("0.00");
            valTempC.Text  = peakTemp.Update(snap.Dashboard.TempC)?.ToString("0") + " °C";

            // Other values
            valDcV.Text   = snap.Dashboard.DcVoltageV?.ToString("0.0") + " V";
            valDcA.Text   = snap.Dashboard.DcCurrentA?.ToString("0.0") + " A";
            valBiasL.Text = snap.Dashboard.BiasLeftV?.ToString("0.00") + " V";
            valBiasR.Text = snap.Dashboard.BiasRightV?.ToString("0.00") + " V";
            valTempRel.Text = snap.Dashboard.TempRel;
            valDiss.Text  = snap.Dashboard.DissipationW?.ToString("0") + " W";
            valBand.Text  = $"{snap.Band.BandLowMhz} – {snap.Band.BandHighMhz}";
            valMode.Text  = snap.Switches.Mode;
            valATU.Text   = snap.ATU.Status;
            valCAT.Text   = snap.Indicators.CatIsActive ? "CAT ON" : "CAT OFF";
            valRC.Text    = snap.Indicators.LastCmdIsRemote ? "RC" : "";

            // Color highlight
            valSWR.ForeColor = (snap.Dashboard.Swr ?? 1.0) > 2.0 ? Color.OrangeRed : Color.LimeGreen;
        }
        catch { }
    }

    private static Label L(string t) => new()
    {
        Text = t,
        AutoSize = true,
        Padding = new Padding(0, 6, 8, 6),
        Font = new Font("Segoe UI", 14f, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = Color.Black
    };

    private static Label LV() => new()
    {
        Text = "-",
        AutoSize = true,
        Font = new Font("Consolas", 18f, FontStyle.Bold),
        ForeColor = Color.DeepSkyBlue,
        BackColor = Color.Black
    };
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
    public AtuInfo ATU { get; init; } = new();
}

public sealed class BandInfo { public string BandLowMhz = ""; public string BandHighMhz = ""; }
public sealed class Indicators { public bool CatIsActive; public bool LastCmdIsRemote; }
public sealed class Switches { public string Mode = ""; }
public sealed class Dashboard
{
    public double? FwdPowerW, RefPowerW, InputPowerW, DissipationW, Swr, GainDb, BiasLeftV, BiasRightV, DcVoltageV, DcCurrentA, TempC;
    public string TempRel = "";
}
public sealed class AtuInfo { public string Status = ""; public double? AtuaSWR; public double? AtuaTempC; }

public static class AmpSnapshotMapper
{
    private static string Get(this IDictionary<string,string> d, string k, string def = "") =>
        d.TryGetValue(k, out var v) ? v : def;

    private static double? Num(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = Regex.Match(s.Replace(',', '.'), @"-?\d+(\.\d+)?");
        return m.Success ? double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    public static AmpSnapshot ToSnapshot(this IDictionary<string,string> d) => new()
    {
        Band = new BandInfo
        {
            BandLowMhz  = d.Get("$amp/controls/dashboard/band/band_low_border_mhz"),
            BandHighMhz = d.Get("$amp/controls/dashboard/band/band_high_border_mhz")
        },
        Indicators = new Indicators
        {
            CatIsActive     = d.Get("$amp/controls/dashboard/indicators/cat_is_active").Contains("ON", StringComparison.OrdinalIgnoreCase),
            LastCmdIsRemote = d.Get("$amp/controls/dashboard/indicators/last_cmd_is_remote").Contains("RC", StringComparison.OrdinalIgnoreCase)
        },
        Switches = new Switches { Mode = d.Get("$amp/controls/dashboard/switches/mode") },
        Dashboard = new Dashboard
        {
            FwdPowerW    = Num(d.Get("$amp/controls/dashboard/values/forward_power")),
            RefPowerW    = Num(d.Get("$amp/controls/dashboard/values/reflected_power")),
            InputPowerW  = Num(d.Get("$amp/controls/dashboard/values/input_power")),
            DissipationW = Num(d.Get("$amp/controls/dashboard/values/dissipated_power")),
            Swr          = Num(d.Get("$amp/controls/dashboard/values/swr")),
            GainDb       = Num(d.Get("$amp/controls/dashboard/values/power_gain")),
            BiasLeftV    = Num(d.Get("$amp/controls/dashboard/values/bias/bias_1a")),
            BiasRightV   = Num(d.Get("$amp/controls/dashboard/values/bias/bias_1b")),
            DcVoltageV   = Num(d.Get("$amp/controls/dashboard/values/hv/hv1")),
            DcCurrentA   = Num(d.Get("$amp/controls/dashboard/values/id/id1")),
            TempC        = Num(d.Get("$amp/controls/dashboard/values/temperature_c")),
            TempRel      = d.Get("$amp/controls/dashboard/values/temperature_rel")
        },
        ATU = new AtuInfo
        {
            Status   = d.Get("$amp/controls/atu/status"),
            AtuaSWR  = Num(d.Get("$amp/controls/atu/measure/values/swr")),
            AtuaTempC= Num(d.Get("$amp/controls/atu/measure/values/temperature"))
        }
    };
}
