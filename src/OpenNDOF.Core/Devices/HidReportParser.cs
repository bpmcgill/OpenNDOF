using OpenNDOF.Core.Input;
using OpenNDOF.Core.Profiles;

namespace OpenNDOF.Core.Devices;

/// <summary>
/// Pure functions for decoding 3DConnexion HID reports into sensor/keyboard state.
/// Kept separate from <see cref="SpaceDevice"/> so the parsing logic is unit-testable
/// without a live HID device.
/// </summary>
internal static class HidReportParser
{
    /// <summary>Decode a signed 16-bit little-endian axis value, normalised to ±1.</summary>
    internal static double ToAxis(byte[] r, int offset)
    {
        short raw = (short)(r[offset] | (r[offset + 1] << 8));
        return raw / 350.0; // 350 ≈ typical full-scale for SpaceNavigator family
    }

    /// <summary>Decode report 0x01 (translation) and merge into the pending sensor state.</summary>
    internal static SensorState ParseTranslation(byte[] r, DeviceProfile profile, SensorState pending)
    {
        double tx = ToAxis(r, 1) * profile.ScaleTx;
        double ty = ToAxis(r, 5) * -profile.ScaleTy;
        double tz = ToAxis(r, 3) * profile.ScaleTz;
        return new SensorState(tx, ty, tz,
            pending.Rx, pending.Ry, pending.Rz);
    }

    /// <summary>Decode report 0x02 (rotation) and merge into the pending sensor state.</summary>
    internal static SensorState ParseRotation(byte[] r, DeviceProfile profile, SensorState pending)
    {
        double rx = ToAxis(r, 1) * profile.ScaleRx;
        double ry = ToAxis(r, 3) * profile.ScaleRy;
        double rz = ToAxis(r, 5) * profile.ScaleRz;
        return new SensorState(
            pending.Tx, pending.Ty, pending.Tz, rx, ry, rz);
    }

    /// <summary>Decode report 0x03 (button bitfield, LSB first, 1 bit per button).</summary>
    internal static IReadOnlySet<int> ParseButtons(byte[] r)
    {
        var pressed = new HashSet<int>();
        for (int byteIdx = 1; byteIdx < r.Length; byteIdx++)
            for (int bit = 0; bit < 8; bit++)
                if ((r[byteIdx] & (1 << bit)) != 0)
                    pressed.Add((byteIdx - 1) * 8 + bit);
        return pressed;
    }
}