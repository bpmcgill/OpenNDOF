# Adding a New Device

This guide walks through adding support for a new 3DConnexion (or compatible) 6-DOF device to OpenNDOF.

---

## 1. Identify VID / PID

Plug the device in and find its USB Vendor ID and Product ID using one of:

- **Device Manager** → right-click the device → Properties → Details → *Hardware IDs*  
  Example: `HID\VID_046D&PID_C62B`
- **USBDeview** (NirSoft freeware)
- **PowerShell:**
  ```powershell
  Get-PnpDevice -Class HIDClass | Where-Object { $_.FriendlyName -like "*3D*" -or $_.FriendlyName -like "*Space*" } | Select-Object FriendlyName, InstanceId
  ```

All 3DConnexion devices use VID `0x046D` (Logitech/3DConnexion). Third-party NDOF devices may use a different VID.

---

## 2. Register the Device

Open `src\OpenNDOF.Core\Devices\KnownDevices.cs` and add an entry to the `All` list:

```csharp
new(0x046D, 0xC62B, "SpaceMouse Pro", DeviceType.SpaceNavigator),
```

### Choosing a `DeviceType`

| Enum value | Use when |
|---|---|
| `SpaceNavigator` | Compact puck, no screen, standard button layout |
| `SpaceExplorer` | Mid-range, extra buttons, no screen |
| `SpacePilot` | Has a 240×64 LCD display |
| `SpaceTraveler` | Ultra-compact form factor |
| `SpaceBall` | SpaceBall 5000 series |
| `Aerion` | Non-3DConnexion NDOF device |

Add a new value to the `DeviceType` enum only if the device has meaningfully different behaviour (e.g. a different LCD resolution or a unique report format) that requires branching in `SpaceDevice`.

---

## 3. Verify the HID Report Format

Almost every 3DConnexion device shares the same report format:

| Report ID | Bytes | Content |
|---|---|---|
| `0x01` | 7 | Translation — `[id, TXlo, TXhi, TZlo, TZhi, TYlo, TYhi]` |
| `0x02` | 7 | Rotation — `[id, RXlo, RXhi, RYlo, RYhi, RZlo, RZhi]` |
| `0x03` | variable | Buttons — 1 bit per button, byte 1 = buttons 0–7, etc. |

> **Note on axis byte order:** TX and TZ are packed before TY in report `0x01`. `SpaceDevice.ParseTranslationReport` already accounts for this. If your device uses a different layout, you may need to adjust the offset constants there.

To confirm, build in **Debug** and watch the Visual Studio Output window while moving the device. You will see raw report bytes logged.

If the device uses a different report structure entirely, add a new `ParseXxxReport` method and dispatch from `OnReportReceived`.

---

## 4. Axis Scaling

Raw axis values are signed 16-bit little-endian integers, normalised by dividing by `350` (the approximate full-scale value for the SpaceNavigator family). If your device saturates earlier or later, adjust the divisor in `SpaceDevice.ToAxis`:

```csharp
private static double ToAxis(byte[] r, int offset)
{
    short raw = (short)(r[offset] | (r[offset + 1] << 8));
    return raw / 350.0; // adjust for your device if needed
}
```

---

## 5. LCD / Display Support

If the device **has no screen**, you are done after steps 1–4.

If the device **has a display**, you need to:

1. Reverse-engineer (or look up) the HID feature-report protocol for that display.
2. Create a new static renderer class (following `SpacePilotLcd.cs` as a template).
3. In `SpaceDevice.TryConnectDevice`, branch on `DeviceInfo.Type` to initialise and write to the display.
4. Expose the display dimensions as `LcdMaxLines` / `LcdCharsPerLine` constants on `SpaceDevice` (or device-specific helpers).

See [lcd-protocol.md](lcd-protocol.md) for a detailed breakdown of the SpacePilot display protocol.

---

## 6. Testing

Run the unit tests:

```powershell
dotnet test tests\OpenNDOF.Tests
```

Add tests in `OpenNDOF.Tests` for any new report-parsing logic. Mock `IHidDevice` is already available in the test project.

---

## 7. Update Documentation

- Add a row to the **Supported Devices** table in `README.md`.
- If you added a new `DeviceType`, update the table in this file too.
