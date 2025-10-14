# ACOM 1400S Live Monitor

A Windows desktop monitor for the **ACOM 1400S amplifier**, built with **C# Avalonia** 
It connects directly to the amplifier’s built-in web interface, extracts live telemetry, and displays it in a **clean dark dashboard**.
 
<img width="1377" height="1027" alt="image" src="https://github.com/user-attachments/assets/2cb0628f-ecca-43bf-9dd9-5c9f9f6dfe14" />


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
- .NET 9.0 or newer 

### Run from source
```bash
git clone https://github.com/yourname/acom1400s-monitor.git
cd acom1400s-monitor
dotnet run --project WinFormsApp1
