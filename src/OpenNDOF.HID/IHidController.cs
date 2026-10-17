namespace OpenNDOF.HID;

/// <summary>
/// Abstraction over HID device enumeration so core code (and tests) can
/// substitute a fake controller instead of the real Win32 singleton.
/// </summary>
public interface IHidController : IDisposable
{
    /// <summary>Raised when HID devices are added or removed.</summary>
    event EventHandler? DevicesChanged;

    /// <summary>Returns a snapshot of all currently enumerated devices.</summary>
    IReadOnlyList<IHidDevice> GetDevices();

    /// <summary>Returns devices matching the given predicate.</summary>
    IEnumerable<IHidDevice> GetDevices(Predicate<IHidDeviceInfo> predicate);
}