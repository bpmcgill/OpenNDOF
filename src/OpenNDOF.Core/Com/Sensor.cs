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
public sealed class Sensor : ISensor, ICustomQueryInterface
{
    private readonly Vector3D  _translation = new();
    private readonly AngleAxis _rotation    = new();
    private          double    _period;
    private          object?   _device;

    // Own connection-point container for the _ISensorEvents outgoing interface.
    private readonly ConnectionPointContainer _events = new(ComIids.SensorEvents, (1, "SensorInput"));

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
    private void FireSensorInput()
    {
        SensorInput?.Invoke();
        _events.Raise(1); // DISPID 1 = SensorInput
    }

    public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
    {
        if (iid == ComIids.IConnectionPointContainer)
        {
            ppv = Marshal.GetComInterfaceForObject(_events, typeof(IConnectionPointContainer));
            return CustomQueryInterfaceResult.Handled;
        }
        ppv = IntPtr.Zero;
        return CustomQueryInterfaceResult.NotHandled;
    }
}
