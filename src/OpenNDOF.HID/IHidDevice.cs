namespace OpenNDOF.HID;

/// <summary>Read-only view of a HID device's identity and capabilities.</summary>
public interface IHidDeviceInfo
{
    string DevicePath  { get; }
    int    VendorId    { get; }
    int    ProductId   { get; }
    int    VersionNumber { get; }
    ushort InputReportLength   { get; }
    ushort OutputReportLength  { get; }
    ushort FeatureReportLength { get; }
}

/// <summary>Live handle to an open HID device.</summary>
public interface IHidDevice : IHidDeviceInfo, IDisposable
{
    event EventHandler<HidReportEventArgs> ReportReceived;

    void   StartReading();
    void   StopReading();
    bool   WriteOutputReport(byte[] report);
    bool   WriteFeatureReport(byte[] report);
    /// <summary>
    /// Writes directly via WriteFile to the interrupt-out endpoint.
    /// This is what hidapi uses and works on devices that do not respond
    /// to HidD_SetOutputReport / HidD_SetFeature IOCTLs.
    /// </summary>
    bool   WriteRawReport(byte[] report);
}

public sealed class HidReportEventArgs(byte[] report) : EventArgs
{
    public byte[] Report { get; } = report;
}
