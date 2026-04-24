# Architecture Overview

This document describes the internal structure of OpenNDOF for contributors and integrators.

---

## Projects

| Project | TFM | Purpose |
|---|---|---|
| `OpenNDOF.HID` | `net10.0-windows` | Raw Win32 HID read/write via P/Invoke |
| `OpenNDOF.Core` | `net10.0-windows` | Device abstraction, report parsing, profiles, LCD |
| `OpenNDOF.App` | `net10.0-windows` | WPF dashboard (WPF-UI Fluent design) |
| `OpenNDOF.Tests` | `net10.0-windows` | xUnit unit tests |

---

## OpenNDOF.HID

Thin wrapper around the Win32 HID API (`hid.dll` + `setupapi.dll`).

```
HidController          — enumerates all HID devices; raises DevicesChanged on WM_DEVICECHANGE
HidDevice              — wraps a single device handle; StartReading / StopReading / WriteFeatureReport
HidReadThread          — background thread that calls ReadFile in a loop
IHidDevice             — interface used throughout Core (mockable in tests)
DeviceAccess           — P/Invoke declarations
Native/NativeApi.cs    — SetupDi / HidD imports
```

`HidController` is a singleton (`HidController.Instance`). It registers a hidden `HwndSource` to receive `WM_DEVICECHANGE` messages and raises `DevicesChanged` when devices are added or removed.

---

## OpenNDOF.Core

### Com

Implements the TDxInput COM API so any application using `CoCreateInstance("TDxInput.Device")` works transparently with OpenNDOF.

| File | Purpose |
|---|---|
| `ComInterfaces.cs` | `[ComVisible]` interface declarations: `ISimpleDevice`, `ISensor`, `IKeyboard`, `ITDxInfo`, `IVector3D`, `IAngleAxis` and their dispinterface event counterparts |
| `Device.cs` | `TDxInput.Device` CoClass — root object created by host apps; subscribes to `SpaceDevice` and bridges HID events into COM |
| `Sensor.cs` | `TDxInput.Sensor` — holds live `Vector3D`/`AngleAxis` values; fires `SensorInput` COM event on each motion report |
| `Keyboard.cs` | `TDxInput.Keyboard` — tracks pressed keys; fires `KeyDown`/`KeyUp` COM events |
| `ValueTypes.cs` | `Vector3D` and `AngleAxis` COM-visible value objects |
| `ComServer.cs` | Static singleton owner of `SpaceDevice`; called by `Device` on `Connect()` |

COM hosting is provided by the .NET runtime's `comhost.dll` (enabled via `<EnableComHosting>true</EnableComHosting>`). No manual class-factory code is needed.

Registration is performed by `Register-ComServer.ps1` (repo root), which writes `HKLM\SOFTWARE\Classes\CLSID` and `ProgID` entries pointing at `TDxInput.comhost.dll`.

### Devices

**`KnownDevices`** — static catalogue mapping `(VendorId, ProductId)` → `SupportedDevice` record + `DeviceType` enum.

**`SpaceDevice`** — the main public API surface:

```
Connect(profileName)        → opens the matching HID device, starts reading, writes LCD greeting
Disconnect()                → stops reading, clears LCD, raises ConnectionChanged
SensorUpdated  (event)      → fired on every translation or rotation report
KeyboardUpdated (event)     → fired when button state changes
WriteDisplayLines(lines)    → renders text to the SpacePilot LCD
IsConnected / DeviceInfo / Sensor / Keyboard   → observable properties (INotifyPropertyChanged)
```

Report dispatch in `OnReportReceived`:

| Report ID | Parser |
|---|---|
| `0x01` | `ParseTranslationReport` → updates `_pendingSensor.Tx/Ty/Tz` |
| `0x02` | `ParseRotationReport` → updates `_pendingSensor.Rx/Ry/Rz`, publishes `SensorUpdated` |
| `0x03` | `ParseButtonReport` → publishes `KeyboardUpdated` on state change |

**`SpacePilotLcd`** — internal static class. See [lcd-protocol.md](lcd-protocol.md) for full details.

### Input

| Class | Description |
|---|---|
| `SensorState` | Immutable 6-axis snapshot (Tx, Ty, Tz, Rx, Ry, Rz), normalised ±1 |
| `KeyboardState` | Immutable set of pressed button indices |

### Profiles

`ProfileManager` loads/saves `List<DeviceProfile>` from `%APPDATA%\OpenNDOF\profiles.json` (System.Text.Json). Profiles contain per-axis scale and deadzone values. The `"default"` profile is always present.

**Error Handling:**
- `Load()` catches specific exception types (JsonException, IOException, ArgumentException) and logs them
- `Save()` validates profile names and wraps I/O operations in try-catch
- `Get()` validates input parameter and throws ArgumentException if null/empty
- `AddOrUpdate()` validates profile before adding
- See [Error Handling Guide](error-handling.md) for detailed documentation

---

## OpenNDOF.App

Built with [WPF-UI](https://github.com/lepoco/wpfui) (Fluent design for WPF). DI is handled with `Microsoft.Extensions.DependencyInjection`.

### Pages

| Page | ViewModel | Purpose |
|---|---|---|
| Dashboard | `DashboardViewModel` | Connection status, live axis bars, button state |
| Test Input | `TestInputViewModel` | 3-D cube responding to 6DOF input |
| Configuration | `ConfigurationViewModel` | Per-axis sliders, profile management |
| LCD Display | `LcdViewModel` | Write text/emoji to SpacePilot LCD |
| About | — | Version / links |

### Service Registration (`App.xaml.cs`)

All services and view-models are registered as singletons or transients. `SpaceDevice` is a singleton and is disposed on application exit. `INavigationViewPageProvider` resolves pages from DI so all views receive their injected VMs automatically.

`ConfigurationViewModel` receives `ProfileManager` and `ISnackbarService`. `SpaceDevice` is **not** injected into it — profile management is independent of the live device connection.

---

## Data Flow

```
USB device
    │  (Win32 ReadFile)
    ▼
HidReadThread  →  HidDevice.ReportReceived (event, background thread)
    │
    ▼
SpaceDevice.OnReportReceived
    ├─ ParseTranslationReport / ParseRotationReport
    │       └─ SensorUpdated event  →  TestInputViewModel / DashboardViewModel
    └─ ParseButtonReport
            └─ KeyboardUpdated event  →  DashboardViewModel
```

All events are raised on the HID background thread. ViewModels must marshal to the UI thread (using `Application.Current.Dispatcher`) before updating observable properties.

---

## Testing

`OpenNDOF.Tests` uses **xUnit**. `IHidDevice` is the main seam — mock it to inject synthetic HID reports without hardware.

```csharp
var mock   = new MockHidDevice();
var device = new SpaceDevice(new MockHidController(mock), new ProfileManager());
device.Connect();

mock.InjectReport(new byte[] { 0x01, 0x60, 0x01, 0x00, 0x00, 0x00, 0x00 });
Assert.True(device.Sensor.Tx > 0);
```
