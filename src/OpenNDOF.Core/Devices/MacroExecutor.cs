using OpenNDOF.Core.Input;
using OpenNDOF.Core.Profiles;

namespace OpenNDOF.Core.Devices;

/// <summary>
/// Executes <see cref="ButtonAction"/> macros when a macro button is pressed.
///
/// Subscribes to <see cref="SpaceDevice.ButtonPressed"/> and looks up the current
/// foreground-app profile via <see cref="ProfileManager.GetByAppName"/>, falling back
/// to the device's <see cref="SpaceDevice.ActiveProfile"/>.
///
/// Actions are fired on the thread-pool so HID parsing is never blocked.
/// <see cref="System.Windows.Forms.SendKeys.SendWait"/> is used for reliable delivery
/// to the active window (GDI+ app, CAD tool, etc.).
/// </summary>
public sealed class MacroExecutor : IDisposable
{
    private readonly SpaceDevice    _device;
    private readonly ProfileManager _profiles;
    private          bool           _attached;
    private          bool           _disposed;

    // Track the current foreground process so we can resolve the right profile
    private string _currentApp = string.Empty;

    public MacroExecutor(SpaceDevice device, ProfileManager profiles)
    {
        _device   = device;
        _profiles = profiles;
    }

    /// <summary>Start listening for button presses. Safe to call multiple times.</summary>
    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        _device.ButtonPressed += OnButtonPressed;
    }

    /// <summary>Stop listening for button presses.</summary>
    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        _device.ButtonPressed -= OnButtonPressed;
    }

    /// <summary>
    /// Called by <see cref="LcdOverlayService"/> (or any foreground monitor) so the
    /// executor always resolves macros against the correct per-app profile.
    /// </summary>
    public void SetCurrentApp(string processName)
        => _currentApp = processName;

    private void OnButtonPressed(object? sender, int buttonIndex)
    {
        // Resolve the best matching profile
        var profile = (!string.IsNullOrEmpty(_currentApp)
                           ? _profiles.GetByAppName(_currentApp)
                           : null)
                      ?? _profiles.Get(_device.ActiveProfile);

        if (profile == null) return;
        if (buttonIndex < 0 || buttonIndex >= profile.ButtonActions.Length) return;

        var action = profile.ButtonActions[buttonIndex];
        if (action.Type == MacroType.None || string.IsNullOrEmpty(action.Keys)) return;

        // Fire on a thread-pool thread so HID parsing is never stalled.
        // SendWait is used so that keystrokes complete before the next macro can fire.
        ThreadPool.QueueUserWorkItem(_ => Execute(action));
    }

    private static void Execute(ButtonAction action)
    {
        try
        {
            switch (action.Type)
            {
                case MacroType.SendKeys:
                    System.Windows.Forms.SendKeys.SendWait(action.Keys);
                    break;

                case MacroType.Text:
                    // Escape special SendKeys characters so the string is sent literally
                    string escaped = EscapeSendKeys(action.Keys);
                    System.Windows.Forms.SendKeys.SendWait(escaped);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Macro] Execute failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes the SendKeys special characters so a literal string is typed.
    /// Special characters: + ^ % ~ ( ) { } [ ]
    /// </summary>
    private static string EscapeSendKeys(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length * 2);
        foreach (char c in text)
        {
            if ("+(^)%~{}[]".Contains(c))
                sb.Append('{').Append(c).Append('}');
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }
}
