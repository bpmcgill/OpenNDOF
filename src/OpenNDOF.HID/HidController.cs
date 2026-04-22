using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OpenNDOF.HID.Native;

namespace OpenNDOF.HID;

/// <summary>
/// Enumerates HID devices and monitors for plug/unplug via a WPF
/// message-only <see cref="HwndSource"/> — no WinForms dependency.
/// </summary>
public sealed class HidController : IDisposable
{
    private static readonly Lazy<HidController> _instance =
        new(() => new HidController(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static HidController Instance => _instance.Value;

    // ── State ────────────────────────────────────────────────────────────────
    private readonly Dictionary<string, HidDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock                          _lock    = new();
    private          Guid                          _hidGuid;
    private          HwndSource?                   _msgWindow;
    private          nint                          _notifyHandle;
    private          bool                          _disposed;

    // ── Events ───────────────────────────────────────────────────────────────
    public event EventHandler? DevicesChanged;

    // ── Construction ─────────────────────────────────────────────────────────
    private HidController()
    {
        NativeApi.HidD_GetHidGuid(ref _hidGuid);
        EnumerateDevices();
        InitNotificationWindow();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Returns a snapshot of all currently enumerated devices.</summary>
    public IReadOnlyList<IHidDevice> GetDevices()
    {
        lock (_lock)
            return [.. _devices.Values];
    }

    /// <summary>Returns devices matching the given predicate.</summary>
    public IEnumerable<IHidDevice> GetDevices(Predicate<IHidDeviceInfo> predicate)
    {
        lock (_lock)
            return _devices.Values.Where(d => predicate(d)).ToList();
    }

    // ── Device enumeration ───────────────────────────────────────────────────

    private void EnumerateDevices()
    {
        lock (_lock)
        {
            foreach (var d in _devices.Values) d.Dispose();
            _devices.Clear();

            nint devInfo = NativeApi.SetupDiGetClassDevs(
                ref _hidGuid, 0, 0,
                NativeApi.DIGCF_DEVICEINTERFACE | NativeApi.DIGCF_PRESENT);

            if (devInfo == -1) return;

            try
            {
                var ifData = new NativeApi.SpDeviceInterfaceData
                    { cbSize = Marshal.SizeOf<NativeApi.SpDeviceInterfaceData>() };

                for (uint i = 0; NativeApi.SetupDiEnumDeviceInterfaces(
                         devInfo, 0, ref _hidGuid, i, ref ifData); i++)
                {
                    string? path = GetDevicePath(devInfo, ref ifData);
                    if (path is null) continue;
                    TryAddDevice(path);
                }
            }
            finally { NativeApi.SetupDiDestroyDeviceInfoList(devInfo); }
        }
    }

    private static string? GetDevicePath(nint devInfo, ref NativeApi.SpDeviceInterfaceData ifData)
    {
        int requiredSize = 0;
        NativeApi.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, 0, 0, ref requiredSize, 0);
        if (requiredSize == 0) return null;

        nint buf = Marshal.AllocHGlobal(requiredSize);
        try
        {
            // cbSize: 8 on 64-bit Windows (DWORD + alignment), 6 on 32-bit
            Marshal.WriteInt32(buf, Environment.Is64BitProcess ? 8 : 6);
            NativeApi.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, buf, requiredSize,
                                                      ref requiredSize, 0);
            return Marshal.PtrToStringAuto(buf + 4);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private void TryAddDevice(string path)
    {
        try
        {
            var dev = new HidDevice(path);
            _devices[path.ToLowerInvariant()] = dev;
        }
        catch { /* device inaccessible — skip */ }
    }

    // ── WM_DEVICECHANGE via WPF HwndSource ───────────────────────────────────

    private void InitNotificationWindow()
    {
        // HwndSource.FromHwnd(0) is not valid; we create a message-only window.
        var p = new HwndSourceParameters("OpenNDOF-HID-Monitor")
        {
            Width = 0, Height = 0,
            WindowStyle = 0,            // no border
            ParentWindow = new nint(-3) // HWND_MESSAGE
        };
        _msgWindow = new HwndSource(p);
        _msgWindow.AddHook(WndProc);

        // Register for HID device notifications
        var filter = new NativeApi.DevBroadcastDeviceInterface
        {
            dbcc_devicetype = NativeApi.DBT_DEVTYP_DEVICEINTERFACE,
            dbcc_classguid  = _hidGuid
        };
        filter.dbcc_size = Marshal.SizeOf(filter);

        nint pFilter = Marshal.AllocHGlobal(filter.dbcc_size);
        try
        {
            Marshal.StructureToPtr(filter, pFilter, false);
            _notifyHandle = NativeApi.RegisterDeviceNotification(
                _msgWindow.Handle, pFilter,
                (uint)NativeApi.DEVICE_NOTIFY_WINDOW_HANDLE);
        }
        finally { Marshal.FreeHGlobal(pFilter); }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeApi.WM_DEVICECHANGE &&
            (wParam == NativeApi.DBT_DEVICEARRIVAL ||
             wParam == NativeApi.DBT_DEVICEREMOVECOMPLETE))
        {
            EnumerateDevices();
            DevicesChanged?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return nint.Zero;
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyHandle != nint.Zero)
            NativeApi.UnregisterDeviceNotification(_notifyHandle);

        _msgWindow?.Dispose();

        lock (_lock)
        {
            foreach (var d in _devices.Values) d.Dispose();
            _devices.Clear();
        }
    }
}
