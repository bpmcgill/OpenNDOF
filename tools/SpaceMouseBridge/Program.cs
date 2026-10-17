using System.Net;
using System.Net.Sockets;
using System.Threading;
using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Input;
using OpenNDOF.Core.Profiles;
using OpenNDOF.HID;

// SpaceMouseBridge — forwards the connected 3Dconnexion device to a Fusion 360
// add-in over localhost UDP.
//
// Wire protocol (compatible with requies/Fusion360-Linux_Spacemouse, MIT):
//   motion :  "m <dx> <dy> <dz> <drx> <dry> <drz> <flags>\n"   (integer deltas,
//             raw 3Dconnexion units ~±350 full deflection; flags unused=0)
//   button :  "b <pressed|0> <button>\n"
//
// Fusion 360 does NOT use the TDxInput COM API — its built-in SpaceMouse
// support requires the 3DxWare driver. This bridge + the Fusion add-in in
// fusion/ drive the viewport directly instead (see README "Fusion 360").
//
// Usage:  SpaceMouseBridge [--port 39030] [--host 127.0.0.1]

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

string host = "127.0.0.1";
int port = 39030;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port" when i + 1 < args.Length && int.TryParse(args[i + 1], out int p): port = p; i++; break;
        case "--host" when i + 1 < args.Length: host = args[i + 1]; i++; break;
        default:
            Console.WriteLine("Usage: SpaceMouseBridge [--host ADDR] [--port PORT]");
            return;
    }
}

using var udp = new UdpClient(AddressFamily.InterNetwork);
var endpoint = new IPEndPoint(IPAddress.Parse(host), port);

Console.WriteLine($"SpaceMouseBridge: forwarding to udp://{host}:{port} (Ctrl+C to stop)");

using var device = new SpaceDevice(HidController.Instance, new ProfileManager());

var bridge = new BridgeState();
device.SensorUpdated   += (_, s) => bridge.OnSensor(s, udp, endpoint);
device.KeyboardUpdated += (_, k) => bridge.OnKeyboard(k, udp, endpoint);

bool wasConnected = false;
var lastStateLog = DateTime.UtcNow;
while (!cts.IsCancellationRequested)
{
    if (!device.IsConnected && !device.Connect())
        Thread.Sleep(2000);

    if (device.IsConnected != wasConnected)
    {
        wasConnected = device.IsConnected;
        Console.WriteLine(wasConnected
            ? $"  connected: {device.DeviceInfo?.FriendlyName} ({device.DeviceInfo?.VendorId:X4}:{device.DeviceInfo?.ProductId:X4})"
            : "  device disconnected — waiting…");
        lastStateLog = DateTime.UtcNow;
    }
    else if (DateTime.UtcNow - lastStateLog > TimeSpan.FromSeconds(5))
    {
        lastStateLog = DateTime.UtcNow;
        Console.WriteLine($"  {bridge.SentPackets} packets sent, {bridge.MotionDeltas} motion deltas, " +
                          $"~{bridge.Hz():0} Hz");
        bridge.ResetRate();
    }
    Thread.Sleep(200);
}

Console.WriteLine("SpaceMouseBridge: stopped.");

/// <summary>Tracks the previous report, converts absolute axes to integer deltas,
/// and diffs button state — feeding the add-in's accumulating packet format.</summary>
sealed class BridgeState
{
    private double[] _last = [0, 0, 0, 0, 0, 0];
    private readonly HashSet<int> _pressed = [];
    private readonly object _lock = new();

    private long _sentPackets;
    private long _motionDeltas;
    private int _rateWindow;
    private DateTime _rateStart = DateTime.UtcNow;

    public long SentPackets => Interlocked.Read(ref _sentPackets);
    public long MotionDeltas => Interlocked.Read(ref _motionDeltas);

    public double Hz()
    {
        var span = (DateTime.UtcNow - _rateStart).TotalSeconds;
        return span > 0 ? _rateWindow / span : 0;
    }

    public void ResetRate() { _rateWindow = 0; _rateStart = DateTime.UtcNow; }

    public void OnSensor(SensorState s, UdpClient udp, IPEndPoint ep)
    {
        double rawToInt = 350.0; // 3Dconnexion raw units (full deflection ≈ ±350)
        double[] cur = [s.Tx, s.Ty, s.Tz, s.Rx, s.Ry, s.Rz];

        int[] deltas = new int[6];
        bool any = false;
        lock (_lock)
        {
            for (int i = 0; i < 6; i++)
            {
                int d = (int)Math.Round((cur[i] - _last[i]) * rawToInt);
                deltas[i] = d;
                any |= d != 0;
            }
            _last = cur;
        }

        if (!any) return; // quiescent — the add-in integrates deltas, skip zeros

        Interlocked.Increment(ref _sentPackets);
        Interlocked.Increment(ref _motionDeltas);
        Interlocked.Increment(ref _rateWindow);
        Send($"m {deltas[0]} {deltas[1]} {deltas[2]} {deltas[3]} {deltas[4]} {deltas[5]} 0\n", udp, ep);
    }

    public void OnKeyboard(KeyboardState k, UdpClient udp, IPEndPoint ep)
    {
        lock (_lock)
        {
            foreach (int code in k.PressedKeys)
            {
                if (_pressed.Add(code))
                {
                    Interlocked.Increment(ref _sentPackets);
                    Send($"b 1 {code}\n", udp, ep);
                }
            }
            foreach (int code in _pressed.ToList())
            {
                if (!k.IsPressed(code))
                {
                    _pressed.Remove(code);
                    Interlocked.Increment(ref _sentPackets);
                    Send($"b 0 {code}\n", udp, ep);
                }
            }
        }
    }

    private static void Send(string text, UdpClient udp, IPEndPoint ep)
        => udp.Send(System.Text.Encoding.ASCII.GetBytes(text), ep);
}