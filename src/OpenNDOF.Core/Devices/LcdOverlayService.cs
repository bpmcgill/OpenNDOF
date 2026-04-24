using OpenNDOF.Core.Profiles;

namespace OpenNDOF.Core.Devices;

/// <summary>
/// Automatically writes the SpacePilot LCD overlay when the foreground application
/// changes.  The overlay shows the active application name as a header banner, then a
/// 2-column × 3-row grid of the 6 macro-button labels from the matching
/// <see cref="DeviceProfile"/>.
///
/// Wire-up: call <see cref="Attach"/> once the device is connected, call
/// <see cref="Detach"/> (or <see cref="Dispose"/>) on disconnect.
/// </summary>
public sealed class LcdOverlayService : IDisposable
{
    private readonly SpaceDevice          _device;
    private readonly ProfileManager       _profiles;
    private readonly ForegroundAppMonitor _monitor;
    private          bool                 _attached;
    private          bool                 _disposed;

    public LcdOverlayService(SpaceDevice device, ProfileManager profiles,
                              ForegroundAppMonitor monitor)
    {
        _device   = device;
        _profiles = profiles;
        _monitor  = monitor;
    }

    /// <summary>Current foreground process name shown on the LCD.</summary>
    public string CurrentApp { get; private set; } = string.Empty;

    /// <summary>Name of the profile that was matched for <see cref="CurrentApp"/>.</summary>
    public string MatchedProfile { get; private set; } = string.Empty;

    /// <summary>Raised (on the monitor thread) whenever the LCD is updated with a new app.</summary>
    public event EventHandler<string>? ForegroundAppChanged;

    /// <summary>
    /// Start watching for foreground app changes and render the initial overlay.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        _monitor.ForegroundAppChanged += OnForegroundChanged;
        // Render immediately for whatever is in front right now
        if (!string.IsNullOrEmpty(_monitor.CurrentApp))
            Render(_monitor.CurrentApp);
    }

    /// <summary>Stop watching and clear the LCD.</summary>
    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        _monitor.ForegroundAppChanged -= OnForegroundChanged;
    }

    private void OnForegroundChanged(object? sender, string processName)
        => Render(processName);

    private void Render(string processName)
    {
        CurrentApp = processName;

        var profile = _profiles.GetByAppName(processName);
        MatchedProfile = profile?.Name ?? string.Empty;

        string appDisplay = processName.Length > 0
            ? char.ToUpperInvariant(processName[0]) + processName[1..]
            : "Unknown";

        string[] labels = profile?.ButtonLabels ?? ["", "", "", "", "", ""];

        _device.WriteButtonGrid(appDisplay, labels);
        ForegroundAppChanged?.Invoke(this, processName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }
}
