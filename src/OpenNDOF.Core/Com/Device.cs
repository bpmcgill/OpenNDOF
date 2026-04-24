using System.Runtime.InteropServices;
using OpenNDOF.Core.Devices;

namespace OpenNDOF.Core.Com;

/// <summary>
/// COM-visible TDxInfo object — returns the driver revision string.
/// CLSID: 1A960ECE-0E57-4A68-B694-8373114F1FF4
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("1A960ECE-0E57-4A68-B694-8373114F1FF4")]
[ProgId("TDxInput.TDxInfo")]
public sealed class TDxInfo : ITDxInfo
{
    public string RevisionNumber() => "10.4.10";   // matches the extracted driver version
}

/// <summary>
/// COM-visible Device object — the root CoClass that host applications create
/// via <c>CoCreateInstance("TDxInput.Device")</c>.
///
/// CLSID: 82C5AB54-C92C-4D52-AAC5-27E25E22604C
/// ProgID: TDxInput.Device
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("82C5AB54-C92C-4D52-AAC5-27E25E22604C")]
[ProgId("TDxInput.Device")]
[ComSourceInterfaces(typeof(_ISimpleDeviceEvents))]
public sealed class Device : ISimpleDevice, IDisposable
{
    private SpaceDevice? _space;
    private bool         _disposed;
    private DateTime     _lastSensorTime = DateTime.UtcNow;

    // ── ISimpleDevice properties ──────────────────────────────────────────────
    public Sensor   Sensor      { get; } = new Sensor();
    public Keyboard Keyboard    { get; } = new Keyboard();
    public int      Type        => 0;
    public bool     IsConnected => _space?.IsConnected ?? false;

    // ── COM event ─────────────────────────────────────────────────────────────
#pragma warning disable CS0067
    public event Action<int>? DeviceChange;
#pragma warning restore CS0067

    // ── ISimpleDevice methods ─────────────────────────────────────────────────

    /// <summary>
    /// Called by the host application to start receiving input.
    /// Locates the shared <see cref="SpaceDevice"/> singleton and subscribes.
    /// </summary>
    public void Connect()
    {
        if (_space is not null) return;

        // Obtain the singleton SpaceDevice via the static accessor on ComServer.
        _space = ComServer.GetDevice();
        if (_space is null) return;

        _space.SensorUpdated   += OnSensor;
        _space.KeyboardUpdated += OnKeyboard;
        _space.ConnectionChanged += OnConnectionChanged;

        if (!_space.IsConnected)
            _space.Connect();

        FireDeviceChange(0);
    }

    public void Disconnect()
    {
        if (_space is null) return;
        _space.SensorUpdated   -= OnSensor;
        _space.KeyboardUpdated -= OnKeyboard;
        _space.ConnectionChanged -= OnConnectionChanged;
        _space = null;
        FireDeviceChange(0);
    }

    public void LoadPreferences(string registryPath) { /* no-op — profiles managed by OpenNDOF */ }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnSensor(object? sender, OpenNDOF.Core.Input.SensorState s)
    {
        double period = (DateTime.UtcNow - _lastSensorTime).TotalSeconds;
        _lastSensorTime = DateTime.UtcNow;
        Sensor.Update(s, period, this);
    }

    private void OnKeyboard(object? sender, OpenNDOF.Core.Input.KeyboardState k)
        => Keyboard.Update(k.PressedKeys, this);

    private void OnConnectionChanged(object? sender, EventArgs e)
        => FireDeviceChange(0);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void FireDeviceChange(int reserved) => DeviceChange?.Invoke(reserved);

    // ── Disposal ──────────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
