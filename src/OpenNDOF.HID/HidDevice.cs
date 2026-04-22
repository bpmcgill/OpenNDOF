using System.IO;
using System.Runtime.InteropServices;
using OpenNDOF.HID.Native;

namespace OpenNDOF.HID;

/// <summary>
/// A live, opened HID device. Handles attribute querying, report reading,
/// and output report writing.
/// </summary>
public sealed class HidDevice : IHidDevice
{
    private readonly DeviceAccess  _ioctl;
    private          HidReadThread? _reader;
    private          DeviceAccess?  _writer;
    private          bool           _disposed;

    public string DevicePath      { get; }
    public int    VendorId        { get; }
    public int    ProductId       { get; }
    public int    VersionNumber   { get; }
    public ushort InputReportLength   { get; }
    public ushort OutputReportLength  { get; }
    public ushort FeatureReportLength { get; }

    public event EventHandler<HidReportEventArgs>? ReportReceived;

    /// <summary>
    /// Opens the device for attribute queries. Throws if the device cannot be opened.
    /// </summary>
    internal HidDevice(string devicePath)
    {
        DevicePath = devicePath;
        _ioctl     = new DeviceAccess(devicePath);

        if (!_ioctl.OpenForIoctl())
            throw new IOException($"Cannot open HID device: {devicePath}");

        // Read attributes (VID / PID / version)
        var attrs = new NativeApi.HiddAttributes { Size = Marshal.SizeOf<NativeApi.HiddAttributes>() };
        NativeApi.HidD_GetAttributes(_ioctl.Handle, ref attrs);
        VendorId      = attrs.VendorID;
        ProductId     = attrs.ProductID;
        VersionNumber = attrs.VersionNumber;

        // Read capabilities (report lengths)
        if (NativeApi.HidD_GetPreparsedData(_ioctl.Handle, out nint preparsed))
        {
            var caps = new NativeApi.HidpCaps();
            NativeApi.HidP_GetCaps(preparsed, ref caps);
            InputReportLength   = caps.InputReportByteLength;
            OutputReportLength  = caps.OutputReportByteLength;
            FeatureReportLength = caps.FeatureReportByteLength;
            NativeApi.HidD_FreePreparsedData(preparsed);
        }
    }

    public void StartReading()
    {
        if (_reader is not null) return;
        _reader               = new HidReadThread(_ioctl);
        _reader.ReportReceived += r => ReportReceived?.Invoke(this, new HidReportEventArgs(r));
        _reader.Start();
    }

    public void StopReading()
    {
        _reader?.Stop();
        _reader?.Dispose();
        _reader = null;
    }

    private DeviceAccess GetWriter()
    {
        if (_writer is null)
        {
            _writer = new DeviceAccess(DevicePath);
            if (!_writer.OpenForWrite())
            {
                _writer.Dispose();
                _writer = null;
                throw new IOException($"Cannot open HID device for writing: {DevicePath}");
            }
        }
        return _writer;
    }

    public bool WriteOutputReport(byte[] report)
    {
        try { return NativeApi.HidD_SetOutputReport(GetWriter().Handle, report, (uint)report.Length); }
        catch { _writer?.Dispose(); _writer = null; return false; }
    }

    public bool WriteFeatureReport(byte[] report)
    {
        try { return NativeApi.HidD_SetFeature(GetWriter().Handle, report, (uint)report.Length); }
        catch { _writer?.Dispose(); _writer = null; return false; }
    }

    public bool WriteRawReport(byte[] report)
    {
        try { return NativeApi.WriteFile(GetWriter().Handle, report, (uint)report.Length, out _, nint.Zero); }
        catch { _writer?.Dispose(); _writer = null; return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopReading();
        _writer?.Dispose();
        _ioctl.Dispose();
    }
}
