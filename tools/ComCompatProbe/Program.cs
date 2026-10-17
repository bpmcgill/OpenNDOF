using System.Runtime.InteropServices;
using OpenNDOF.Core.Com;

// Diagnoses why CAD applications (SolidWorks, AutoCAD, Maya, ...) see the
// OpenNDOF TDxInput COM server but receive no sensor/button events.
//
// TDxInput clients subscribe to events (SensorInput, KeyDown/KeyUp) through
// COM connection points (IConnectionPointContainer). If the CCW does not
// expose them, an event-driven client like SolidWorks gets no data at all.
//
// Run:  dotnet run --project tools/ComCompatProbe

static void ProbeEventSource(string name, object obj, Type iface, params Guid[] eventIids)
{
    nint pUnk = Marshal.GetComInterfaceForObject(obj, iface);
    Console.WriteLine($"--- {name} ---");
    Guid icpc = new("B196B286-BAB4-101A-B69C-00AA00341D07"); // IConnectionPointContainer
    int hr = Marshal.QueryInterface(pUnk, ref icpc, out nint pCpc);
    Console.WriteLine($"QueryInterface(IConnectionPointContainer): 0x{hr:X8} {(hr == 0 ? "OK" : "E_NOINTERFACE")}");
    if (hr == 0)
    {
        var cpc = (IConnectionPointContainer)Marshal.GetTypedObjectForIUnknown(
            pCpc, typeof(IConnectionPointContainer));
        foreach (var eid in eventIids)
        {
            Guid riid = eid;
            try
            {
                cpc.FindConnectionPoint(ref riid, out nint pCp);
                Console.WriteLine($"  FindConnectionPoint({eid}): OK - event source exposed");
                Marshal.Release(pCp);
            }
            catch (COMException ex)
            {
                Console.WriteLine($"  FindConnectionPoint({eid}): 0x{ex.HResult:X8} not exposed");
            }
        }
        Marshal.Release(pCpc);
    }
    else
    {
        Console.WriteLine("  => CCW does NOT implement IConnectionPointContainer.");
        Console.WriteLine("     Native clients (SolidWorks/Solid Edge/etc.) cannot receive SensorInput/KeyDown/KeyUp.");
        Console.WriteLine("     This is a .NET Core runtime limitation for [ComSourceInterfaces] on the server side.");
    }
    Marshal.Release(pUnk);
}

Guid sensorEvents   = new("E6929A4A-6F41-46C6-9252-A8CC53472CB1"); // _ISensorEvents
Guid keyboardEvents = new("6B6BB0A8-4491-40CF-B1A9-C15A801FE151"); // _IKeyboardEvents
Guid deviceEvents   = new("8FE3A216-E235-49A6-9136-F9D81FDADEF5"); // _ISimpleDeviceEvents

ProbeEventSource("Sensor",   new Sensor(),   typeof(ISensor),       sensorEvents);
ProbeEventSource("Keyboard", new Keyboard(), typeof(IKeyboard),     keyboardEvents);
ProbeEventSource("Device",   new Device(),   typeof(ISimpleDevice), deviceEvents);

[ComImport]
[Guid("B196B286-BAB4-101A-B69C-00AA00341D07")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IConnectionPointContainer
{
    void EnumConnectionPoints(out IntPtr ppEnum);
    void FindConnectionPoint(ref Guid riid, out IntPtr ppCP);
}