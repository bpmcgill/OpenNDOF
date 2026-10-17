using OpenNDOF.HID;

namespace OpenNDOF.UnitTests;

/// <summary>In-memory <see cref="IHidDevice"/> for injecting synthetic HID reports.</summary>
internal sealed class FakeHidDevice : IHidDevice
{
    public string    DevicePath    { get; init; } = "";
    public int       VendorId      { get; init; }
    public int       ProductId     { get; init; }
    public int       VersionNumber { get; init; }
    public ushort    InputReportLength   { get; init; }
    public ushort    OutputReportLength  { get; init; }
    public ushort    FeatureReportLength { get; init; }

    public bool Started { get; private set; }
    public List<byte[]> OutputReports  { get; } = [];
    public List<byte[]> FeatureReports { get; } = [];
    public List<byte[]> RawReports     { get; } = [];

    public event EventHandler<HidReportEventArgs>? ReportReceived;

    public void StartReading() => Started = true;
    public void StopReading() => Started = false;

    public bool WriteOutputReport(byte[] report) { OutputReports.Add(report); return true; }
    public bool WriteFeatureReport(byte[] report) { FeatureReports.Add(report); return true; }
    public bool WriteRawReport(byte[] report) { RawReports.Add(report); return true; }

    /// <summary>Simulates the device delivering one HID report.</summary>
    public void InjectReport(byte[] report)
        => ReportReceived?.Invoke(this, new HidReportEventArgs(report));

    public void Dispose() { }
}

/// <summary>In-memory <see cref="IHidController"/> that returns the fake devices added to it.</summary>
internal sealed class FakeHidController : IHidController
{
    private readonly List<IHidDevice> _devices = [];

    public event EventHandler? DevicesChanged;

    public IReadOnlyList<IHidDevice> Devices => _devices;

    public void Add(IHidDevice device) => _devices.Add(device);

    public IReadOnlyList<IHidDevice> GetDevices() => [.. _devices];

    public IEnumerable<IHidDevice> GetDevices(Predicate<IHidDeviceInfo> predicate)
        => _devices.Where(d => predicate(d)).ToList();

    public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() { }
}