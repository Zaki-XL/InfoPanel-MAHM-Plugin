# InfoPanel MSI Afterburner (MAHM) Plugin

English | [日本語 (Japanese)](README.md)

A high-performance, crash-free **MSI Afterburner Shared Memory Plugin** for [InfoPanel](https://infopanel.net) (v1.4+).
Reads real-time hardware metrics directly from MSI Afterburner's shared memory (`MAHMSharedMemory`) and presents them cleanly and reliably on InfoPanel.

---

## Background & Advantages

Traditional plugins often suffer from application crashes due to internal errors in native GPU monitoring libraries (such as NVML).
This plugin reads aggregated telemetry from Windows Shared Memory in a read-only manner, completely isolating InfoPanel from direct hardware manipulation and ensuring **100% stability and zero host crashes**.

---

## Key Features

- **Crash-Free Architecture**:
  Completely avoids risky kernel/driver calls by reading memory-mapped data populated by MSI Afterburner.
- **Hierarchical Clean Containers**:
  Automatically organizes extensive Afterburner telemetry into 6 clean, non-redundant containers:
  - **`GPU`** : Utilization, VRAM usage (`Memory usage` [MB]), VRAM usage % (`FB usage (VRAM Usage)` [%]), temperature, fan speeds, core/memory clocks, power, status.
  - **`GPU - Advanced & Limits`** : VID/BUS usage, thermal/power throttling limit flags.
  - **`CPU`** : Overall CPU usage, package temperature, clock speed, power, status.
  - **`CPU - Cores`** : Per-core temperatures, usage, and clocks (CPU1–CPU20+).
  - **`Memory`** : System RAM usage and commit charge.
  - **`Gaming (RTSS)`** : Framerate (FPS), frametime (ms), Min/Avg/Max FPS, and gaming status.
- **Smart N/A Handling & Sentinel Value Normalization**:
  - Automatically identifies RTSS sentinel values (`FLT_MAX` / `3.4028235E+38`) when no game is active, normalizing them to `float.NaN` and `"N/A"`.
  - Gaming status activates (`Active`) only when true 3D rendering is detected (`Framerate >= 1.0 FPS`), falling back cleanly to `Idle (No Game Detected)` with `N/A` displays when idle.
- **Framerate Min / Max Scope**:
  - Reflects the minimum and maximum FPS captured across the active game session (or benchmark hotkey recording interval).
- **Automated Test Suite (100% Pass)**:
  - 30 comprehensive automated tests covering header validation, sentinel filtering, corrupted entries, extreme values, concurrency, and live Afterburner integration.

> [!NOTE]
> **Sensor List Changes**:
> In InfoPanel's architecture, sensor lists are registered when InfoPanel launches. If you enable or disable monitoring metrics in MSI Afterburner, please **restart InfoPanel** to reload the updated sensor list.

---

## Installation (2 Easy Steps)

### Step 1: Copy Plugin Directory
Copy the [`release/InfoPanel.MAHM`](release/InfoPanel.MAHM) folder directly to:

```text
C:\ProgramData\InfoPanel\plugins\
```

Expected directory tree:
```text
C:\ProgramData\InfoPanel\plugins\
└── InfoPanel.MAHM\
    ├── InfoPanel.MAHM.dll
    └── PluginInfo.ini
```

### Step 2: Restart InfoPanel
Restart InfoPanel. **`MSI Afterburner`** will now appear in your sensor sources list.

---

## Related Links
- [InfoPanel Official Website](https://infopanel.net)
- [InfoPanel GitHub Repository](https://github.com/habibrehmansg/infopanel)
- [MSI Afterburner Official Website](https://www.msi.com/Landing/afterburner/graphics-cards)

---

## Building & Testing

### Prerequisites
- .NET 8.0 SDK (Windows)
- InfoPanel 1.4+
- MSI Afterburner

### Build
```powershell
dotnet build InfoPanel.MAHM.sln -c Release
```

### Test
```powershell
dotnet test InfoPanel.MAHM.sln -c Release
```

---

## License
MIT License
