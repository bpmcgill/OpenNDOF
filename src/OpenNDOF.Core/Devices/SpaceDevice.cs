using CommunityToolkit.Mvvm.ComponentModel;
using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Input;
using OpenNDOF.Core.Profiles;
using OpenNDOF.HID;

namespace OpenNDOF.Core.Devices;

/// <summary>
/// Connects to a supported 6-DOF HID device, parses its reports,
/// and exposes live <see cref="SensorState"/> and <see cref="KeyboardState"/>.
/// </summary>
public sealed class SpaceDevice : ObservableObject, IDisposable
{
    private readonly HidController      _hid;
    private readonly ProfileManager     _profiles;
    private readonly ForegroundAppMonitor _appMonitor;
    private          LcdOverlayService?  _overlay;
    private          MacroExecutor?      _macros;
    private          IHidDevice?         _device;
    private          IHidDevice?         _lcdDevice;
    private          bool                _disposed;

    // ── Observable state ─────────────────────────────────────────────────────
    private SupportedDevice?  _deviceInfo;
    private bool              _isConnected;
    private SensorState       _sensor   = SensorState.Zero;
    private KeyboardState     _keyboard = KeyboardState.Empty;
    private string            _activeProfile = "default";

    public SupportedDevice?  DeviceInfo      { get => _deviceInfo;    private set => SetProperty(ref _deviceInfo, value); }
    public bool              IsConnected     { get => _isConnected;   private set => SetProperty(ref _isConnected, value); }
    public SensorState       Sensor          { get => _sensor;        private set => SetProperty(ref _sensor, value); }
    public KeyboardState     Keyboard        { get => _keyboard;      private set => SetProperty(ref _keyboard, value); }
    public string            ActiveProfile   { get => _activeProfile; private set => SetProperty(ref _activeProfile, value); }

    // ── Events ───────────────────────────────────────────────────────────────
    public event EventHandler<SensorState>?   SensorUpdated;
    public event EventHandler<KeyboardState>? KeyboardUpdated;
    /// <summary>
    /// Raised once per button press (leading edge only, not on hold or release).
    /// The argument is the zero-based button index (0–5 for the SpacePilot macro buttons).
    /// </summary>
    public event EventHandler<int>?           ButtonPressed;
    public event EventHandler?                ConnectionChanged;

    public SpaceDevice(HidController hid, ProfileManager profiles)
    {
        _hid        = hid;
        _profiles   = profiles;
        _appMonitor = new ForegroundAppMonitor();
        _appMonitor.Start();
        _hid.DevicesChanged += OnDevicesChanged;
    }

    // ── Connection ───────────────────────────────────────────────────────────

    public bool Connect(string profileName = "default")
    {
        ActiveProfile = profileName;
        _profiles.Load();
        return TryConnectDevice();
    }

    public void Disconnect()
    {
        if (_device is not null)
        {
            _device.StopReading();
            _device.ReportReceived -= OnReportReceived;
            _device = null;
        }
        _overlay?.Detach();
        _overlay?.Dispose();
        _overlay = null;

        _appMonitor.ForegroundAppChanged -= OnForegroundAppChangedForMacros;
        _macros?.Detach();
        _macros?.Dispose();
        _macros = null;

        if (_lcdDevice is not null)
        {
            SpacePilotLcd.Clear(_lcdDevice);
            _lcdDevice = null;
        }
        DeviceInfo  = null;
        IsConnected = false;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryConnectDevice()
    {
        Disconnect();

        var match = _hid.GetDevices(info =>
            KnownDevices.Match(info.VendorId, info.ProductId) is not null)
            .FirstOrDefault();

        if (match is null) return false;

        _device               = match;
        _device.ReportReceived += OnReportReceived;
        _device.StartReading();

        DeviceInfo  = KnownDevices.Match(match.VendorId, match.ProductId)!;
        IsConnected = true;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

        // Find the LCD HID interface — same VID/PID but a different top-level
        // collection that exposes feature reports (FeatureReportLength >= 8).
        var allMatchingInterfaces = _hid.GetDevices(info =>
            info.VendorId  == match.VendorId  &&
            info.ProductId == match.ProductId)
            .ToList();

        System.Diagnostics.Debug.WriteLine($"[LCD] SpacePilot HID interfaces ({allMatchingInterfaces.Count}):");
        foreach (var iface in allMatchingInterfaces)
            System.Diagnostics.Debug.WriteLine(
                $"[LCD]   Path={iface.DevicePath}  " +
                $"In={iface.InputReportLength}  Out={iface.OutputReportLength}  Feat={iface.FeatureReportLength}");

        // The SpacePilot has a single HID interface handling both input reports and
        // feature reports. Reuse _device for LCD writes rather than opening a second handle.
        _lcdDevice = allMatchingInterfaces.Any(d => d.FeatureReportLength >= 8) ? _device : null;

        // Start LCD overlay for SpacePilot
        if (DeviceInfo.Type == DeviceType.SpacePilot && _lcdDevice is not null)
        {
            _overlay = new LcdOverlayService(this, _profiles, _appMonitor);
            _overlay.Attach();
        }

        // Start macro executor for all device types
        _macros = new MacroExecutor(this, _profiles);
        _macros.Attach();
        // Keep the executor in sync with whatever the overlay is tracking
        if (_appMonitor.CurrentApp is { } app)
            _macros.SetCurrentApp(app);
        _appMonitor.ForegroundAppChanged += OnForegroundAppChangedForMacros;

        return true;
    }

    private DateTime _lastConnectAttempt = DateTime.MinValue;
    private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromSeconds(3);

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        // Ignore if already connected and the device is still present
        if (IsConnected && _device is not null) return;

        // Throttle reconnect attempts to avoid a WM_DEVICECHANGE feedback loop
        if (DateTime.UtcNow - _lastConnectAttempt < ReconnectCooldown) return;
        _lastConnectAttempt = DateTime.UtcNow;

        TryConnectDevice();
    }

    // ── HID report parsing ───────────────────────────────────────────────────

    private void OnReportReceived(object? sender, HidReportEventArgs e)
    {
        var report = e.Report;
        if (report.Length < 1) return;

        switch (report[0])
        {
            case 0x01 when report.Length >= 7:
                ParseTranslationReport(report);
                break;
            case 0x02 when report.Length >= 7:
                ParseRotationReport(report);
                break;
            case 0x03 when report.Length >= 3:
                ParseButtonReport(report);
                break;
        }
    }

    private SensorState _pendingSensor = SensorState.Zero;

    private void ParseTranslationReport(byte[] r)
    {
        var profile = _profiles.Get(ActiveProfile);
        double tx = ToAxis(r, 1) * profile.ScaleTx;
        double ty = ToAxis(r, 5) * -profile.ScaleTy;
        double tz = ToAxis(r, 3) * profile.ScaleTz;
        _pendingSensor = new SensorState(tx, ty, tz,
            _pendingSensor.Rx, _pendingSensor.Ry, _pendingSensor.Rz);
        PublishSensor(_pendingSensor);
    }

    private void ParseRotationReport(byte[] r)
    {
        var profile = _profiles.Get(ActiveProfile);
        double rx = ToAxis(r, 1) * profile.ScaleRx;
        double ry = ToAxis(r, 3) * profile.ScaleRy;
        double rz = ToAxis(r, 5) * profile.ScaleRz;
        _pendingSensor = new SensorState(
            _pendingSensor.Tx, _pendingSensor.Ty, _pendingSensor.Tz, rx, ry, rz);
        PublishSensor(_pendingSensor);
    }

    private HashSet<int> _publishedButtons = [];

    private void ParseButtonReport(byte[] r)
    {
        var pressed = new HashSet<int>();
        for (int byteIdx = 1; byteIdx < r.Length; byteIdx++)
            for (int bit = 0; bit < 8; bit++)
                if ((r[byteIdx] & (1 << bit)) != 0)
                    pressed.Add((byteIdx - 1) * 8 + bit);

        if (pressed.SetEquals(_publishedButtons)) return;

        // Fire ButtonPressed for each button that just went down (leading edge)
        foreach (int idx in pressed)
            if (!_publishedButtons.Contains(idx))
                ButtonPressed?.Invoke(this, idx);

        _publishedButtons = [.. pressed];
        var ks = new KeyboardState(_publishedButtons);
        Keyboard = ks;
        KeyboardUpdated?.Invoke(this, ks);
    }

    private void PublishSensor(SensorState s)
    {
        SensorUpdated?.Invoke(this, s);
    }

    /// <summary>Decode a signed 16-bit little-endian axis value, normalised to ±1.</summary>
    private static double ToAxis(byte[] r, int offset)
    {
        short raw = (short)(r[offset] | (r[offset + 1] << 8));
        return raw / 350.0; // 350 ≈ typical full-scale for SpaceNavigator family
    }

    // ── SpacePilot LCD ───────────────────────────────────────────────────────

    public const int LcdMaxLines    = SpacePilotLcd.MaxLines;
    public const int LcdCharsPerLine = SpacePilotLcd.CharsPerLine;

    public bool WriteDisplayLines(params string[] lines)
    {
        if (DeviceInfo?.Type != DeviceType.SpacePilot) return false;
        if (_lcdDevice is not null)
            return SpacePilotLcd.WriteText(_lcdDevice, lines);
        return false;
    }

    public bool WriteButtonGrid(string appName, string[] labels)
    {
        if (DeviceInfo?.Type != DeviceType.SpacePilot) return false;
        if (_lcdDevice is not null)
            return SpacePilotLcd.WriteButtonGrid(_lcdDevice, appName, labels);
        return false;
    }

    /// <summary>The overlay service — exposes CurrentApp and MatchedProfile for the UI.</summary>
    public LcdOverlayService? Overlay => _overlay;

    // ── Disposal ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hid.DevicesChanged -= OnDevicesChanged;
        Disconnect();
        _appMonitor.Dispose();
    }

    private void OnForegroundAppChangedForMacros(object? sender, string processName)
        => _macros?.SetCurrentApp(processName);
}
