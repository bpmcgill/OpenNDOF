namespace OpenNDOF.Core.Input;

/// <summary>
/// Immutable snapshot of the translation and rotation axes reported
/// by a 6-DOF input device in a single HID report.
/// </summary>
public sealed class SensorState
{
    public static readonly SensorState Zero = new(0, 0, 0, 0, 0, 0);

    public double Tx { get; }   // Translation X  (left/right)
    public double Ty { get; }   // Translation Y  (up/down)
    public double Tz { get; }   // Translation Z  (forward/back)
    public double Rx { get; }   // Rotation X (pitch)
    public double Ry { get; }   // Rotation Y (yaw)
    public double Rz { get; }   // Rotation Z (roll)

    public SensorState(double tx, double ty, double tz, double rx, double ry, double rz)
    {
        Tx = tx; Ty = ty; Tz = tz;
        Rx = rx; Ry = ry; Rz = rz;
    }

    /// <summary>Apply per-axis scale factors from a device profile.</summary>
    public SensorState WithScale(
        double sx, double sy, double sz,
        double srx, double sry, double srz) =>
        new(Tx * sx, Ty * sy, Tz * sz, Rx * srx, Ry * sry, Rz * srz);

    public bool IsZero() =>
        Math.Abs(Tx) < 0.001 && Math.Abs(Ty) < 0.001 && Math.Abs(Tz) < 0.001 &&
        Math.Abs(Rx) < 0.001 && Math.Abs(Ry) < 0.001 && Math.Abs(Rz) < 0.001;

    public override string ToString() =>
        $"T({Tx:F2}, {Ty:F2}, {Tz:F2})  R({Rx:F2}, {Ry:F2}, {Rz:F2})";
}
