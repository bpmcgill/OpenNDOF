using System.Runtime.InteropServices;

// ComTestHarness — a real TDxInput COM client, exactly like SolidWorks.
//
// It activates TDxInput.Device through the REGISTERED comhost (no reference to
// OpenNDOF assemblies), subscribes to SensorInput/KeyDown/KeyUp through COM
// connection points, and prints a live readout. If a CAD application does not
// work, this isolates whether the COM server itself is healthy.
//
// Usage:  ComTestHarness [seconds]     (default 8)
// Exit:   0 = healthy; 1 = server reachable but event delivery broken;
//         2 = TDxInput.Device not registered.

int failures = 0;
void Check(string label, bool ok)
{
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} - {label}");
    if (!ok) failures++;
}

// ── 1. Activation through the registered COM server ────────────────────────
Console.WriteLine("== ComTestHarness (TDxInput COM client) ==");
Type? t = Type.GetTypeFromProgID("TDxInput.Device");
if (t is null)
{
    Console.WriteLine("FAIL - TDxInput.Device is not registered.");
    Console.WriteLine("       Run the OpenNDOF installer, or from an admin shell:");
    Console.WriteLine("         cd <build output>; .\\Register-ComServer.ps1");
    Environment.Exit(2);
}
var device = (ISimpleDevice)Activator.CreateInstance(t)!;
Console.WriteLine("PASS - activated TDxInput.Device through the registered comhost");

// ── 2. Capabilities ─────────────────────────────────────────────────────────
Console.WriteLine($"      Type = {device.Type}, IsConnected = {device.IsConnected}, " +
                  $"ProgrammableKeys = {device.Keyboard.ProgrammableKeys}");

// ── 3. Connect ──────────────────────────────────────────────────────────────
device.Connect();
Console.WriteLine($"      after Connect(): IsConnected = {device.IsConnected}");

// ── 4. Subscribe to COM events via connection points ───────────────────────
var sensorSink = new SensorSink();
var keyboardSink = new KeyboardSink();
AttachEvents("Sensor", device.Sensor, typeof(ISensor), sensorSink, TdxIids.SensorEvents, typeof(_ISensorEvents));
AttachEvents("Keyboard", device.Keyboard, typeof(IKeyboard), keyboardSink, TdxIids.KeyboardEvents, typeof(_IKeyboardEvents));

// ── 5. Live readout ─────────────────────────────────────────────────────────
int seconds = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 8;
Console.WriteLine();
Console.WriteLine($"== Sampling for {seconds}s — move the device, press buttons ==");
var deadline = DateTime.UtcNow.AddSeconds(seconds);
string? last = null;
while (DateTime.UtcNow < deadline)
{
    var sensor = device.Sensor;
    var tvec = sensor.Translation;
    var rvec = sensor.Rotation;
    string line = $"T({tvec.X,7:0.00},{tvec.Y,7:0.00},{tvec.Z,7:0.00}) " +
                  $"R({rvec.X,7:0.00},{rvec.Y,7:0.00},{rvec.Z,7:0.00}) " +
                  $"| SensorInput:{sensorSink.Count}";
    if (line != last)
    {
        Console.WriteLine(line);
        last = line;
    }
    Thread.Sleep(120);
}

Console.WriteLine();
if (device.IsConnected)
{
    Check($"SensorInput events received ({sensorSink.Count} in {seconds}s)", sensorSink.Count > 0);
}
else
{
    Check("device is connected", false);
    Console.WriteLine("      No device found on this machine — connect the SpaceMouse and retry.");
}
Console.WriteLine(failures == 0
    ? "RESULT: COM server healthy — events are flowing to a client."
    : $"RESULT: {failures} check(s) failed — see above.");
Environment.Exit(failures == 0 ? 0 : 1);

// ── Helpers ─────────────────────────────────────────────────────────────────

void AttachEvents(string name, object source, Type sourceIface, object sink,
                  Guid eventIid, Type sinkIface)
{
    nint pSource = Marshal.GetComInterfaceForObject(source, sourceIface);
    nint pSink   = Marshal.GetComInterfaceForObject(sink, sinkIface);
    try
    {
        Guid iidCpc = TdxIids.ConnectionPointContainer;
        int hr = Marshal.QueryInterface(pSource, in iidCpc, out nint pCpc);
        if (hr != 0)
        {
            Check($"{name}: IConnectionPointContainer (0x{hr:X8})", false);
            return;
        }
        var cpc = (IConnectionPointContainer)Marshal.GetTypedObjectForIUnknown(
            pCpc, typeof(IConnectionPointContainer));
        Guid riid = eventIid;
        cpc.FindConnectionPoint(ref riid, out nint pCp);
        var cp = (IConnectionPoint)Marshal.GetTypedObjectForIUnknown(pCp, typeof(IConnectionPoint));
        cp.Advise(pSink, out int cookie);
        Check($"{name}: connection point subscribed (cookie {cookie})", true);
        Marshal.Release(pCp);
        Marshal.Release(pCpc);
    }
    finally
    {
        Marshal.Release(pSink);
        Marshal.Release(pSource);
    }
}

// ── Event sinks (the "client application" side of the connection point) ─────

[ComVisible(true)]
public sealed class SensorSink : _ISensorEvents
{
    public int Count;
    public void SensorInput() => Count++;
}

[ComVisible(true)]
public sealed class KeyboardSink : _IKeyboardEvents
{
    public readonly List<int> Down = [];
    public readonly List<int> Up = [];
    public void KeyDown(int keyCode) { Down.Add(keyCode); Console.WriteLine($"      KeyDown {keyCode}"); }
    public void KeyUp(int keyCode) { Up.Add(keyCode); }
}

// ── COM identifiers ─────────────────────────────────────────────────────────

internal static class TdxIids
{
    public static readonly Guid ConnectionPointContainer = new("B196B286-BAB4-101A-B69C-00AA00341D07");
    public static readonly Guid SensorEvents = new("E6929A4A-6F41-46C6-9252-A8CC53472CB1");
    public static readonly Guid KeyboardEvents = new("6B6BB0A8-4491-40CF-B1A9-C15A801FE151");
}

// ── TDxInput interfaces (official IIDs, mirror of 3DxInput.tlb) ─────────────
// Dual interfaces are consumed (ComImport); event interfaces are implemented
// (plain ComVisible sinks) — mirroring how the original 3DxInput.h is used.

[ComImport]
[Guid("CB3BF65E-0816-482A-BB11-64AF1E837812")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface ISimpleDevice
{
    void Connect();
    void Disconnect();
    ISensor Sensor { get; }
    IKeyboard Keyboard { get; }
    int Type { get; }
    bool IsConnected { get; }
    void LoadPreferences(string registryPath);
}

[ComImport]
[Guid("F3A6775E-6FA1-4829-BF32-5B045C29078F")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface ISensor
{
    IVector3D Translation { get; }
    IAngleAxis Rotation { get; }
    object Device { get; }
    double Period { get; }
}

[ComImport]
[Guid("D6F968E7-2993-48D7-AF24-8B602D925B2C")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IKeyboard
{
    int Keys { get; }
    int ProgrammableKeys { get; }
    string GetKeyLabel(int keyCode);
    string GetKeyName(int keyCode);
    object Device { get; }
    bool IsKeyDown(int keyCode);
    bool IsKeyUp(int keyCode);
}

[ComImport]
[Guid("8C2AA71D-2B23-43F5-A6ED-4DF57E9CD8D5")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IVector3D
{
    double X { get; set; }
    double Y { get; set; }
    double Z { get; set; }
    double Length { get; set; }
}

[ComImport]
[Guid("1EF2BAFF-54E9-4706-9F61-078F7134FD35")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IAngleAxis
{
    double X { get; set; }
    double Y { get; set; }
    double Z { get; set; }
    double Angle { get; set; }
}

[ComVisible(true)]
[Guid("E6929A4A-6F41-46C6-9252-A8CC53472CB1")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface _ISensorEvents
{
    [DispId(1)] void SensorInput();
}

[ComVisible(true)]
[Guid("6B6BB0A8-4491-40CF-B1A9-C15A801FE151")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface _IKeyboardEvents
{
    [DispId(1)] void KeyDown(int keyCode);
    [DispId(2)] void KeyUp(int keyCode);
}

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