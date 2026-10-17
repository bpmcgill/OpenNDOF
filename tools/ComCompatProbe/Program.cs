using System.Runtime.InteropServices;
using OpenNDOF.Core.Com;
using OpenNDOF.Core.Input;

// Diagnoses TDxInput COM compatibility with native CAD applications
// (SolidWorks, Solid Edge, AutoCAD, Inventor, Maya, ...).
//
// Those clients subscribe to events (SensorInput, KeyDown/KeyUp, DeviceChange)
// through COM connection points (IConnectionPointContainer). Before the
// connection-point implementation, .NET Core CCWs answered E_NOINTERFACE for
// IConnectionPointContainer, so event-driven clients received no data at all.
//
// Run:  dotnet run --project tools/ComCompatProbe

int failures = 0;
void Check(string label, bool ok)
{
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} - {label}");
    if (!ok) failures++;
}

// Sensor (ISensor -> _ISensorEvents.SensorInput)
{
    Console.WriteLine("--- Sensor (ISensor -> _ISensorEvents.SensorInput) ---");
    var sensor = new Sensor();
    nint pUnk = Marshal.GetComInterfaceForObject(sensor, typeof(ISensor));
    int hr = Marshal.QueryInterface(pUnk, in ComIids.IConnectionPointContainer, out nint pCpc);
    Marshal.Release(pUnk);
    Check($"IConnectionPointContainer exposed (0x{hr:X8})", hr == 0);
    if (hr == 0)
    {
        var cpc = (IConnectionPointContainer)Marshal.GetTypedObjectForIUnknown(pCpc, typeof(IConnectionPointContainer));
        Guid riid = ComIids.SensorEvents;
        cpc.FindConnectionPoint(ref riid, out nint pCp);
        var cp = (IConnectionPoint)Marshal.GetTypedObjectForIUnknown(pCp, typeof(IConnectionPoint));
        var sink = new ProbeSensorSink();
        nint pSink = Marshal.GetComInterfaceForObject(sink, typeof(_ISensorEvents));
        cp.Advise(pSink, out _);

        sensor.Update(new SensorState(1, 0, 0, 0, 0, 0), 0.016, new object());
        sensor.Update(new SensorState(0, 1, 0, 0, 0, 0), 0.016, new object());

        Check($"SensorInput delivered to sink ({sink.SensorInputCount} of 2)", sink.SensorInputCount == 2);
        Marshal.Release(pSink);
        Marshal.Release(pCp);
        Marshal.Release(pCpc);
    }
}

// Keyboard (IKeyboard -> _IKeyboardEvents.KeyDown/KeyUp)
{
    Console.WriteLine("--- Keyboard (IKeyboard -> _IKeyboardEvents.KeyDown/KeyUp) ---");
    var keyboard = new Keyboard();
    nint pUnk = Marshal.GetComInterfaceForObject(keyboard, typeof(IKeyboard));
    int hr = Marshal.QueryInterface(pUnk, in ComIids.IConnectionPointContainer, out nint pCpc);
    Marshal.Release(pUnk);
    Check($"IConnectionPointContainer exposed (0x{hr:X8})", hr == 0);
    if (hr == 0)
    {
        var cpc = (IConnectionPointContainer)Marshal.GetTypedObjectForIUnknown(pCpc, typeof(IConnectionPointContainer));
        Guid riid = ComIids.KeyboardEvents;
        cpc.FindConnectionPoint(ref riid, out nint pCp);
        var cp = (IConnectionPoint)Marshal.GetTypedObjectForIUnknown(pCp, typeof(IConnectionPoint));
        var sink = new ProbeKeyboardSink();
        nint pSink = Marshal.GetComInterfaceForObject(sink, typeof(_IKeyboardEvents));
        cp.Advise(pSink, out _);

        keyboard.Update([1, 2], new object());
        keyboard.Update([2], new object());

        Check($"KeyDown delivered ({string.Join(",", sink.Down)})", sink.Down.SequenceEqual([1, 2]));
        Check($"KeyUp delivered ({string.Join(",", sink.Up)})", sink.Up.SequenceEqual([1]));
        Marshal.Release(pSink);
        Marshal.Release(pCp);
        Marshal.Release(pCpc);
    }
}

// Device (ISimpleDevice -> _ISimpleDeviceEvents.DeviceChange);
// the raise path needs a connected device and is covered by unit tests.
{
    Console.WriteLine("--- Device (ISimpleDevice -> _ISimpleDeviceEvents.DeviceChange) ---");
    var device = new Device();
    nint pUnk = Marshal.GetComInterfaceForObject(device, typeof(ISimpleDevice));
    int hr = Marshal.QueryInterface(pUnk, in ComIids.IConnectionPointContainer, out nint pCpc);
    Marshal.Release(pUnk);
    Check($"IConnectionPointContainer exposed (0x{hr:X8})", hr == 0);
    if (hr == 0)
        Marshal.Release(pCpc);
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "RESULT: ALL CHECKS PASSED - COM events are deliverable to native clients."
    : $"RESULT: {failures} CHECK(S) FAILED.");

// Native-style event sinks: their CCWs act as the "client" that subscribes
// through IConnectionPointContainer, exactly like SolidWorks etc. do.

[ComVisible(true)]
public sealed class ProbeSensorSink : _ISensorEvents
{
    public int SensorInputCount;
    public void SensorInput() => SensorInputCount++;
}

[ComVisible(true)]
public sealed class ProbeKeyboardSink : _IKeyboardEvents
{
    public readonly List<int> Down = [];
    public readonly List<int> Up = [];
    public void KeyDown(int keyCode) => Down.Add(keyCode);
    public void KeyUp(int keyCode) => Up.Add(keyCode);
}

[ComVisible(true)]
public sealed class ProbeDeviceSink : _ISimpleDeviceEvents
{
    public readonly List<int> Changes = [];
    public void DeviceChange(int reserved) => Changes.Add(reserved);
}