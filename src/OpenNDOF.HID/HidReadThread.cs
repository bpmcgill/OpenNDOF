using OpenNDOF.HID.Native;

namespace OpenNDOF.HID;

/// <summary>
/// Reads HID input reports on a dedicated background thread,
/// firing <see cref="ReportReceived"/> for each report.
/// </summary>
internal sealed class HidReadThread : IDisposable
{
    private readonly DeviceAccess               _device;
    private readonly CancellationTokenSource    _cts = new();
    private readonly Thread                     _thread;
    private bool                                _disposed;

    public event Action<byte[]>? ReportReceived;

    public HidReadThread(DeviceAccess device)
    {
        _device = device;
        _thread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name         = $"HID-Read [{device.Path[..Math.Min(40, device.Path.Length)]}]"
        };
    }

    public void Start() => _thread.Start();

    private void ReadLoop()
    {
        // Re-open the handle owned by this thread (separate from the IOCTL handle).
        using var access = new DeviceAccess(_device.Path);
        if (!access.OpenForRead()) return;

        // Report length is read from the IOCTL handle's caps.
        // We use a generous buffer; actual data length is fixed by the device.
        const int maxReport = 64;
        var buffer = new byte[maxReport];

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                int bytesRead = 0;
                bool ok = ReadFile(access.Handle.DangerousGetHandle(), buffer, (uint)buffer.Length,
                                   ref bytesRead, nint.Zero);
                if (!ok || bytesRead == 0) continue;

                var report = new byte[bytesRead];
                Array.Copy(buffer, report, bytesRead);
                ReportReceived?.Invoke(report);
            }
            catch (OperationCanceledException) { break; }
            catch { /* device unplugged — loop will exit next iteration */ break; }
        }
    }

    public void Stop() => _cts.Cancel();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(nint hFile, byte[] lpBuffer,
        uint nNumberOfBytesToRead, ref int lpNumberOfBytesRead, nint lpOverlapped);
}
