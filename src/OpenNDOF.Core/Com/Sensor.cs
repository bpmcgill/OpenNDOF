using System.Runtime.InteropServices;
using OpenNDOF.Core.Input;

namespace OpenNDOF.Core.Com;

/// <summary>
/// COM-visible Sensor object. Fires the <c>SensorInput</c> dispinterface event
/// each time the device sends a motion report.
/// CLSID: 85004B00-1AA7-4777-B1CE-8427301B942D
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("85004B00-1AA7-4777-B1CE-8427301B942D")]
[ProgId("TDxInput.Sensor")]
[ComSourceInterfaces(typeof(_ISensorEvents))]
public sealed class Sensor : ISensor
{
    private readonly Vector3D  _translation = new();
    private readonly AngleAxis _rotation    = new();
    private          double    _period;
    private          object?   _device;

    // ── ISensor ───────────────────────────────────────────────────────────────
    public IVector3D  Translation => _translation;
    public IAngleAxis Rotation    => _rotation;
    public object     Device      => _device!;
    public double     Period      => _period;

    // ── COM event ─────────────────────────────────────────────────────────────
#pragma warning disable CS0067  // raised via COM event infrastructure
    public event Action? SensorInput;
#pragma warning restore CS0067

    // ── Internal update called by Device ──────────────────────────────────────
    internal void Update(SensorState s, double periodSeconds, object device)
    {
        _device  = device;
        _period  = periodSeconds;
        _translation.Set(s.Tx, s.Ty, s.Tz);
        _rotation.Set(s.Rx, s.Ry, s.Rz);
        FireSensorInput();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void FireSensorInput() => SensorInput?.Invoke();
}
