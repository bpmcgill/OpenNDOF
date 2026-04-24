# OpenNDOF

**OpenNDOF** is an open-source Windows bridge for 3DConnexion 6-DOF (six degrees-of-freedom) input devices. It reads raw HID reports directly — no proprietary driver or SDK required — and exposes live sensor and button data via a clean .NET API and a WPF dashboard application.

---

## Features

- **Zero-driver operation** — communicates directly with the HID interface; the 3DConnexion driver does not need to be installed.
- **TDxInput COM server** — registers as a drop-in replacement for the official 3DConnexion COM server so AutoCAD, SolidWorks, Blender, Maya and any other TDxInput-aware application works transparently.
- **Live 6-axis readout** — Translation (TX/TY/TZ) and Rotation (RX/RY/RZ) at full device rate.
- **Button events** — all device buttons reported as a `KeyboardState` snapshot.
- **Named profiles** — per-axis sensitivity scaling and dead-zone, stored in `%APPDATA%\OpenNDOF\profiles.json`.
- **SpacePilot LCD support** — 240×64 monochrome display driven via reverse-engineered HID feature reports; full Unicode and emoji support.
- **WPF dashboard** — real-time axis bars, 3-D viewport cube, LCD page, and configuration sliders.

---

## Supported Devices

| Device | VID | PID | LCD |
|---|---|---|---|
| SpaceNavigator | `046D` | `C626` | — |
| SpaceExplorer | `046D` | `C627` | — |
| SpacePilot | `046D` | `C625` | ✔ 240×64 |
| SpaceTraveler | `046D` | `C623` | — |
| SpaceBall 5000 | `046D` | `C621` / `C622` | — |
| Aerion NDOF | `03EB` | `2013` | — |

> **Adding a new device:** See [Adding a New Device](#adding-a-new-device) below.

---

## Requirements

- Windows 10 / 11 (64-bit)
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- The device must be plugged in via USB; Bluetooth is not supported.

---

## Getting Started

### Build from source

```powershell
git clone https://github.com/bpmcgill/OpenNDOF.git
cd OpenNDOF
dotnet build
```

### Run the dashboard

```powershell
dotnet run --project src\OpenNDOF.App
```

Plug in your device, navigate to **Dashboard**, and click **Connect**.

### Use the library in your own app

Add a reference to `OpenNDOF.Core` and `OpenNDOF.HID`:

```csharp
using OpenNDOF.Core.Devices;
using OpenNDOF.HID;

var hid      = HidController.Instance;
var profiles = new ProfileManager();
var device   = new SpaceDevice(hid, profiles);

device.SensorUpdated   += (_, s) => Console.WriteLine(s);
device.KeyboardUpdated += (_, k) => Console.WriteLine(string.Join(", ", k.PressedKeys));

device.Connect();
Console.ReadLine();
device.Dispose();
```

---

## Architecture

```
OpenNDOF.HID          — raw Win32 HID read/write (P/Invoke, no external deps)
OpenNDOF.Core
  ├─ Com/
  │   ├─ ComInterfaces.cs  — [ComVisible] interface definitions (ISimpleDevice, ISensor, IKeyboard…)
  │   ├─ Device.cs         — TDxInput.Device CoClass (root COM object)
  │   ├─ Sensor.cs         — TDxInput.Sensor CoClass + SensorInput event
  │   ├─ Keyboard.cs       — TDxInput.Keyboard CoClass + KeyDown/KeyUp events
  │   ├─ ValueTypes.cs     — Vector3D / AngleAxis COM value objects
  │   └─ ComServer.cs      — shared SpaceDevice singleton for COM activation
  ├─ Devices/
  │   ├─ KnownDevices.cs    — VID/PID catalogue + DeviceType enum
  │   ├─ SpaceDevice.cs     — connection, report parsing, profile application
  │   └─ SpacePilotLcd.cs   — 240×64 LCD renderer (GDI → 1-bit page format)
  ├─ Input/
  │   ├─ SensorState.cs     — immutable 6-axis snapshot
  │   └─ KeyboardState.cs   — immutable button snapshot
  └─ Profiles/
      └─ ProfileManager.cs  — JSON persistence, named profiles
OpenNDOF.App          — WPF UI (WPF-UI / Fluent design)
OpenNDOF.Tests        — unit tests
```

### HID report format (3DConnexion common protocol)

| Report ID | Length | Meaning |
|---|---|---|
| `0x01` | 7 bytes | Translation axes (TX, TY, TZ as signed 16-bit LE) |
| `0x02` | 7 bytes | Rotation axes (RX, RY, RZ as signed 16-bit LE) |
| `0x03` | variable | Button bitfield (1 bit per button, LSB first) |

### SpacePilot LCD protocol

The 240×64 display is driven with HID feature reports:

| Report ID | Payload | Purpose |
|---|---|---|
| `0x12` | `[id, 0, 0, 0x2F, ...]` | Suppress firmware redraws |
| `0x0C` | `[id, page, col, 0]` | Set write cursor (page 0–7, col 0–239) |
| `0x0D` | `[id, c0…c6]` | Write 7 column bytes; bit 0 = top row of page |

Each page is a horizontal band of 8 pixel rows. Text is rendered with GDI `TextRenderer` (for emoji fallback) to a 32bpp `Bitmap`, thresholded to 1-bit, then packed into the page/column format before transmission.

---

## Using with Other Applications (COM Server)

AutoCAD, SolidWorks, Blender, Maya, and many other 3D applications communicate
with 3DConnexion devices through the **TDxInput COM API**. OpenNDOF implements
this API and can be registered as the system-wide COM server so these applications
receive input from your device through OpenNDOF — **no 3DxWare driver needed**.

### Register (run once, as Administrator)

```powershell
# From the OpenNDOF.Core build output directory:
.\Register-ComServer.ps1
```

This writes the required `HKLM\SOFTWARE\Classes\CLSID` and `ProgID` registry keys
pointing at `TDxInput.comhost.dll` (the .NET COM host built alongside the project).

### Unregister

```powershell
.\Register-ComServer.ps1 -Unregister
```

### How it works

When a host application calls `CoCreateInstance("TDxInput.Device")`, Windows loads
`TDxInput.comhost.dll` which activates the .NET `Device` COM class. That class
connects to the shared `SpaceDevice` singleton and forwards live HID data as
standard `ISensor.SensorInput` and `IKeyboard.KeyDown`/`KeyUp` COM events — exactly
what the official driver would have sent.

| COM Object | CLSID | Purpose |
|---|---|---|
| `TDxInput.Device` | `82C5AB54-...` | Root object; host apps `CoCreateInstance` this |
| `TDxInput.Sensor` | `85004B00-...` | Fires `SensorInput` with 6-axis data |
| `TDxInput.Keyboard` | `25BBE090-...` | Fires `KeyDown` / `KeyUp` per button |
| `TDxInput.TDxInfo` | `1A960ECE-...` | Reports driver revision string |

---

## Configuration Profiles

Profiles are stored in `%APPDATA%\OpenNDOF\profiles.json`.

```json
[
  {
    "Name": "default",
    "ScaleTx": 1.0, "ScaleTy": 1.0, "ScaleTz": 1.0,
    "DeadzoneTrans": 0.05,
    "ScaleRx": 1.0, "ScaleRy": 1.0, "ScaleRz": 1.0,
    "DeadzoneRot": 0.05
  }
]
```

- **Scale** values multiply the normalised axis value (±1 full scale).
- **Deadzone** suppresses small movements below the threshold.

---

## Adding a New Device

1. **Find the VID/PID.** Plug the device in and check Device Manager → Details → Hardware IDs, or use a tool like USBDeview.

2. **Add it to `KnownDevices.cs`:**

   ```csharp
   new(0x046D, 0xC62B, "SpaceMouse Pro", DeviceType.SpaceNavigator),
   ```

   Reuse an existing `DeviceType` if the HID report format matches, or add a new enum value if it differs.

3. **Verify the report format.** Connect the device, run the app in Debug, and watch the Output window for `[LCD]` / raw report traces. The 3DConnexion common protocol (reports `0x01`/`0x02`/`0x03`) is shared across almost all devices without a screen.

4. **LCD support** (if the device has a display): implement a renderer similar to `SpacePilotLcd.cs` and call it from `SpaceDevice.TryConnectDevice` when `DeviceInfo.Type` matches.

5. Add a row to the [Supported Devices](#supported-devices) table in this README.

---

## Error Handling and Robustness

OpenNDOF implements comprehensive exception handling to provide a crash-free experience:

- **Profile I/O:** Load failures gracefully recover with defaults; save failures are logged and reported to the user
- **Input Validation:** Profile names are validated at entry points; invalid inputs fail fast with clear error messages
- **Macro Execution:** Button press errors are isolated and logged without affecting HID polling
- **User Feedback:** All user-facing operations provide snackbar notifications (success, error, and detailed messages)
- **Diagnostics:** Structured debug logging for troubleshooting and development

See [Error Handling Guide](docs/error-handling.md) for detailed documentation on exception strategies and best practices.

---

Pull requests are welcome. Please:

- Keep changes focused on a single concern per PR.
- Add or update unit tests in `OpenNDOF.Tests` for any logic changes.
- Do not commit `%APPDATA%\OpenNDOF\profiles.json` or any generated files under `obj/`.

---

## License

The majority of this project is released under the **MIT License** — see [LICENSE](LICENSE).

### Third-party components

| Component | License | Notes |
|---|---|---|
| `OpenNDOF.HID` | MIT **or** LGPL 2.1 | See [NOTICE](NOTICE) — if any HID enumeration code was derived from the Aerion SpaceNav Win32 driver, those files are LGPL 2.1 |
| LCD protocol (SpacePilotLcd.cs) | MIT | Protocol reverse-engineered by [jtsiomb/3dxdisp](https://github.com/jtsiomb/3dxdisp) (GPL v2); no source copied |

See [NOTICE](NOTICE) for full attribution details.
