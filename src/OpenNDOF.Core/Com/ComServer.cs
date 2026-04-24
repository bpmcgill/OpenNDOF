using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Profiles;
using OpenNDOF.HID;

namespace OpenNDOF.Core.Com;

/// <summary>
/// Static bootstrap for the COM server lifetime.
///
/// When <c>TDxInput.dll</c> (this assembly) is activated as an in-process COM
/// server, the CLR COM host calls <c>DllGetClassObject</c> automatically via
/// the <c>EnableComHosting</c> project setting.  No manual class-factory code
/// is required for .NET 5+ COM hosting.
///
/// This class owns the single <see cref="SpaceDevice"/> instance that all
/// <see cref="Device"/> COM objects share.
/// </summary>
public static class ComServer
{
    private static SpaceDevice?   _device;
    private static ProfileManager? _profiles;
    private static readonly object _lock = new();

    /// <summary>
    /// Returns the shared <see cref="SpaceDevice"/>, creating it on first call.
    /// Thread-safe.
    /// </summary>
    internal static SpaceDevice GetDevice()
    {
        lock (_lock)
        {
            if (_device is not null) return _device;
            _profiles = new ProfileManager();
            _profiles.Load();
            _device   = new SpaceDevice(HidController.Instance, _profiles);
            _device.Connect();
            return _device;
        }
    }

    /// <summary>
    /// Shuts down the shared device. Call from application exit or test teardown.
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            _device?.Dispose();
            _device   = null;
            _profiles = null;
        }
    }
}
