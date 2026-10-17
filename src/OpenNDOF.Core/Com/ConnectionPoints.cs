using System.Runtime.InteropServices;

namespace OpenNDOF.Core.Com;

// ── Native COM interfaces we implement on behalf of the CCW ─────────────────
// The .NET Core runtime does not provide IConnectionPointContainer for
// [ComSourceInterfaces] on the server side (CCW), so native TDxInput clients
// (SolidWorks, Solid Edge, AutoCAD, ...) can never receive SensorInput /
// KeyDown / KeyUp / DeviceChange events.  We implement the connection-point
// machinery ourselves and hand it out via ICustomQueryInterface on the
// COM-visible classes.

[ComImport]
[Guid("B196B286-BAB4-101A-B69C-00AA00341D07")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IConnectionPointContainer
{
    void EnumConnectionPoints(out IntPtr ppEnum);
    void FindConnectionPoint(ref Guid riid, out IntPtr ppCP);
}

[ComImport]
[Guid("B196B28E-BAB4-101A-B69C-00AA00341D07")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IConnectionPoint
{
    void GetConnectionInterface(out Guid pIID);
    void GetConnectionPointContainer(out IConnectionPointContainer ppCPC);
    void Advise(IntPtr pUnkSink, out int pdwCookie);
    void Unadvise(int dwCookie);
    void EnumConnections(out IntPtr ppEnum);
}

public static class ComIids
{
    public static readonly Guid IConnectionPointContainer = new("B196B286-BAB4-101A-B69C-00AA00341D07");
    public static readonly Guid IConnectionPoint          = new("B196B28E-BAB4-101A-B69C-00AA00341D07");
    public static readonly Guid IDispatch                 = new("00020400-0000-0000-C000-000000000046");

    // Outgoing (event) interfaces.
    public static readonly Guid SensorEvents       = new("E6929A4A-6F41-46C6-9252-A8CC53472CB1");
    public static readonly Guid KeyboardEvents     = new("6B6BB0A8-4491-40CF-B1A9-C15A801FE151");
    public static readonly Guid SimpleDeviceEvents = new("8FE3A216-E235-49A6-9136-F9D81FDADEF5");
}

/// <summary>
/// Single-outgoing-interface connection point container.  The COM-visible
/// classes (Sensor/Keyboard/Device) own one instance each and return it from
/// <c>ICustomQueryInterface.GetInterface</c> for IID_IConnectionPointContainer.
/// </summary>
public sealed class ConnectionPointContainer : IConnectionPointContainer, IDisposable
{
    private readonly ConnectionPoint _point;

    public ConnectionPointContainer(Guid eventIid, params (int DispId, string Name)[] events)
        => _point = new ConnectionPoint(eventIid, this, events);

    public bool HasSink => _point.HasSink;

    public void EnumConnectionPoints(out IntPtr ppEnum)
        => throw new COMException("Not implemented", unchecked((int)0x80004001)); // E_NOTIMPL

    public void FindConnectionPoint(ref Guid riid, out IntPtr ppCP)
    {
        if (riid != _point.EventIid)
            throw new COMException("Interface not supported", unchecked((int)0x80004002)); // E_NOINTERFACE
        ppCP = Marshal.GetComInterfaceForObject(_point, typeof(IConnectionPoint));
    }

    /// <summary>Invoke the connected sink (if any) for the given disp-id.</summary>
    public void Raise(int dispId, params object?[]? args) => _point.Raise(dispId, args);

    public void Dispose() => _point.Dispose();
}

/// <summary>
/// One IConnectionPoint: holds the client's sink and raises events on it.
/// Events are delivered to the sink's IDispatch (dispinterface sinks), looked
/// up by method name so the client's own DISPID mapping is used.
/// </summary>
public sealed class ConnectionPoint : IConnectionPoint, IDisposable
{
    private const int E_NOTIMPL = unchecked((int)0x80004001);

    private readonly Guid                       _eventIid;
    private readonly ConnectionPointContainer   _container;
    private readonly IReadOnlyDictionary<int, string> _events;
    private          object?                    _sink;    // RCW (native sink) or unwrapped managed object
    private          int                        _cookie;
    private          int                        _nextCookie = 1;

    public ConnectionPoint(Guid eventIid, ConnectionPointContainer container,
                           params (int DispId, string Name)[] events)
    {
        _eventIid  = eventIid;
        _container = container;
        _events    = events.ToDictionary(e => e.DispId, e => e.Name);
    }

    public Guid EventIid => _eventIid;
    public bool HasSink => _sink is not null;

    public void GetConnectionInterface(out Guid pIID) => pIID = _eventIid;

    public void GetConnectionPointContainer(out IConnectionPointContainer ppCPC) => ppCPC = _container;

    public void Advise(IntPtr pUnkSink, out int pdwCookie)
    {
        if (pUnkSink == 0) throw new ArgumentNullException(nameof(pUnkSink));

        Guid iidDispatch = ComIids.IDispatch;
        int hr = Marshal.QueryInterface(pUnkSink, in iidDispatch, out nint pDispatch);
        if (hr != 0)
            throw new COMException("Sink does not support IDispatch", hr);

        Unadvise(0); // single sink per event interface — replace any previous connection
        try
        {
            _sink = Marshal.GetObjectForIUnknown(pDispatch);
        }
        finally
        {
            Marshal.Release(pDispatch); // the RCW holds its own reference
        }
        _cookie = _nextCookie++;
        pdwCookie = _cookie;
    }

    public void Unadvise(int dwCookie)
    {
        _sink = null;
        _cookie = 0;
    }

    public void EnumConnections(out IntPtr ppEnum)
        => throw new COMException("Not implemented", E_NOTIMPL);

    public void Raise(int dispId, object?[]? args)
    {
        if (_sink is not { } sink) return;
        if (!_events.TryGetValue(dispId, out string? name)) return;

        // Dynamic dispatch: for a native client this goes through its
        // IDispatch::GetIDsOfNames/Invoke; for same-process managed sinks it
        // calls the method directly.  Never let a client failure break the
        // HID pipeline.
        try
        {
            dynamic d = sink;
            switch (name)
            {
                case "SensorInput":
                    d.SensorInput();
                    break;
                case "KeyDown":
                    d.KeyDown(Arg(0, args));
                    break;
                case "KeyUp":
                    d.KeyUp(Arg(0, args));
                    break;
                case "DeviceChange":
                    d.DeviceChange(Arg(0, args));
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"[ComEvents] Unknown event '{name}' for {_eventIid}");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ComEvents] Sink invoke failed for {name}: {ex.Message}");
        }
    }

    private static int Arg(int index, object?[]? args)
        => args is not null && index < args.Length && args[index] is int v ? v : 0;

    public void Dispose() => Unadvise(0);
}