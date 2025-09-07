# ACOM 1400S Live Monitor

A Windows desktop monitor for the **ACOM 1400S amplifier**, built with **C# WinForms** and **WebView2**.  
It connects directly to the amplifier’s built-in web interface, extracts live telemetry, and displays it in a **clean dark dashboard**.


<img width="986" height="676" alt="image" src="https://github.com/user-attachments/assets/c797a793-2571-4d45-b244-1d4601ce3f37" />


---

## ✨ Features

- 📊 Real-time dashboard with:
  - Forward / Reflected Power
  - SWR
  - Input Power
  - Power Gain
  - DC Voltage / Current
  - Bias (L/R)
  - Temperature (absolute + relative)
  - Dissipation
  - Band, Mode, ATU, CAT, RC indicators
- 🔒 Peak-hold logic (values remain visible for ~3 seconds before resetting)
- 🌙 Dark theme with bold high-contrast labels
- 🚀 Lightweight and self-contained (no browser required at runtime)

---

## 🛠️ Getting Started

### Prerequisites
- Windows 10/11
- .NET 8.0 or newer
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

### Run from source
```bash
git clone https://github.com/yourname/acom1400s-monitor.git
cd acom1400s-monitor
dotnet run --project WinFormsApp1
