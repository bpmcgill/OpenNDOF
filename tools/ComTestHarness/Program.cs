using System.IO;
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
//         2 = TDxInput.Device not registered / comhost missing.
//
// Member access is done via IDispatch (dynamic), like a scripted client,
// because same-process CCW interop unwraps returned objects to their managed
// types — typed interface casts across assemblies fail on those even when the
// COM IIDs match. Connection points are driven by raw IID/pointer QI instead.
// The main thread is STA (like CAD apps); OpenNDOF's HID monitor no longer
// depends on the caller's apartment, so MTA clients work too.

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        int failures = 0;
        void Check(string label, bool ok)
        {
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} - {label}");
            if (!ok) failures++;
        }

        // ── 1. Activation through the registered COM server ─────────────────
        Console.WriteLine("== ComTestHarness (TDxInput COM client) ==");
        Type? t = Type.GetTypeFromProgID("TDxInput.Device");
        if (t is null)
        {
            Console.WriteLine("FAIL - TDxInput.Device is not registered.");
            Console.WriteLine("       Run the OpenNDOF installer, or from an admin shell:");
            Console.WriteLine("         cd <build output>; .\\Register-ComServer.ps1");
            Environment.Exit(2);
        }

        // The ProgID may still be registered while the installed comhost is
        // missing (e.g. an interrupted uninstall) — fail fast with a clear
        // message instead of the cryptic 0x8007007E from CoCreateInstance.
        string? comhost = (string?)Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{82C5AB54-C92C-4D52-AAC5-27E25E22604C}\InprocServer32",
            "", null);
        if (string.IsNullOrEmpty(comhost) || !File.Exists(comhost))
        {
            Console.WriteLine(comhost is null
                ? "FAIL - TDxInput.Device is registered but the comhost value is missing."
                : $"FAIL - TDxInput.Device is registered at '{comhost}' but that file does not exist.");
            Console.WriteLine("       Re-run the OpenNDOF installer (OpenNDOF-Setup.exe) and retry.");
            Environment.Exit(2);
        }

        dynamic device = Activator.CreateInstance(t)!;
        Console.WriteLine("PASS - activated TDxInput.Device through the registered comhost");

        // ── 2. Capabilities ─────────────────────────────────────────────────
        Console.WriteLine($"      Type = {device.Type}, IsConnected = {device.IsConnected}, " +
                          $"ProgrammableKeys = {device.Keyboard.ProgrammableKeys}");

// ── 3. Connect ──────────────────────────────────────────────────────────────
        device.Connect();
        Console.WriteLine($"      after Connect(): IsConnected = {device.IsConnected}");
        Console.WriteLine($"      Type = {device.Type}, ProgrammableKeys = {device.Keyboard.ProgrammableKeys} " +
                          "(expect non-zero when a known device is connected)");

        dynamic sensor = device.Sensor;
        dynamic keyboard = device.Keyboard;
        var sensorSink = new SensorSink();
        var keyboardSink = new KeyboardSink();
        AttachEvents("Sensor", (object)sensor, sensorSink, TdxIids.SensorEvents, typeof(_ISensorEvents));
        AttachEvents("Keyboard", (object)keyboard, keyboardSink, TdxIids.KeyboardEvents, typeof(_IKeyboardEvents));

        // ── 4. Live readout ─────────────────────────────────────────────────
        int seconds = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 8;
        Console.WriteLine();
        Console.WriteLine($"== Sampling for {seconds}s ==");
        Console.WriteLine(">> MOVE THE PUCK and press buttons NOW (the devices sends no data at rest). <<");
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        string? last = null;
        while (DateTime.UtcNow < deadline)
        {
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
    }

    private static void AttachEvents(string name, object source, object sink, Guid eventIid, Type sinkIface)
    {
        // In-process the source unwraps to its managed object; GetIDispatchForObject
        // returns its IDispatch pointer for the QI. The connection-point objects
        // also unwrap to managed instances, so drive them through dynamic after
        // the real COM QI (cross-assembly typed casts fail on unwrapped objects).
        nint pSource = Marshal.GetIDispatchForObject(source);
        nint pSink = Marshal.GetComInterfaceForObject(sink, sinkIface);
        try
        {
            Guid iidCpc = TdxIids.ConnectionPointContainer;
            int hr = Marshal.QueryInterface(pSource, in iidCpc, out nint pCpc);
            if (hr != 0)
            {
                Console.WriteLine($"  FAIL - {name}: IConnectionPointContainer (0x{hr:X8})");
                return;
            }
            try
            {
                dynamic cpc = Marshal.GetObjectForIUnknown(pCpc)!;
                Guid riid = eventIid;
                cpc.FindConnectionPoint(ref riid, out nint pCp);
                dynamic cp = Marshal.GetObjectForIUnknown(pCp)!;
                cp.Advise(pSink, out int cookie);
                Console.WriteLine($"  PASS - {name}: connection point subscribed (cookie {cookie})");
                Marshal.Release(pCp);
            }
            finally
            {
                Marshal.Release(pCpc);
            }
        }
        finally
        {
            Marshal.Release(pSink);
            Marshal.Release(pSource);
        }
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

// ── Event sink interfaces (official IIDs) and connection points ─────────────

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