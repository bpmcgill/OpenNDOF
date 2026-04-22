using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using OpenNDOF.HID.Native;

namespace OpenNDOF.HID;

/// <summary>
/// Wraps a Win32 CreateFile handle for a single HID device path,
/// supporting three access modes (IOCTL, Read, Write).
/// </summary>
internal sealed class DeviceAccess : IDisposable
{
    private SafeFileHandle? _handle;
    private bool            _disposed;

    public string         Path   { get; }
    public SafeFileHandle Handle => _handle ?? throw new InvalidOperationException("Device not open.");

    public DeviceAccess(string path) => Path = path;

    /// <summary>Opens with zero desired-access for attribute queries (IOCTL).</summary>
    public bool OpenForIoctl() => Open(0, FileShare.ReadWrite);

    /// <summary>Opens for reading HID input reports.</summary>
    public bool OpenForRead()  => Open(0x80000000 /* GENERIC_READ */, FileShare.ReadWrite);

    /// <summary>Opens for writing HID output/feature reports (GENERIC_READ|GENERIC_WRITE, matching hidapi).</summary>
    public bool OpenForWrite() => Open(0x80000000 | 0x40000000 /* GENERIC_READ | GENERIC_WRITE */, FileShare.ReadWrite);

    private bool Open(uint access, FileShare share)
    {
        _handle?.Dispose();
        _handle = NativeApi.CreateFile(Path, access, (uint)share, 0, (uint)FileMode.Open, 0, 0);
        return _handle is { IsInvalid: false };
    }

    public void Close()
    {
        _handle?.Dispose();
        _handle = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle?.Dispose();
    }
}
