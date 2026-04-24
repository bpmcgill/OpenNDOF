using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenNDOF.Core.Devices;

/// <summary>
/// Watches for foreground window changes using <c>SetWinEventHook</c> and
/// raises <see cref="ForegroundAppChanged"/> whenever the active process changes.
/// Runs entirely on the thread that calls <see cref="Start"/> (must be an STA
/// thread with a message pump, or the hook is self-contained via a background thread).
/// </summary>
public sealed class ForegroundAppMonitor : IDisposable
{
    // ── Win32 ────────────────────────────────────────────────────────────────
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT   = 0x0000;

    private delegate void WinEventProc(
        nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(
        uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    // ── State ────────────────────────────────────────────────────────────────
    private nint           _hook;
    private WinEventProc?  _procRef;   // keep alive — delegate must not be GC'd
    private string         _lastApp  = string.Empty;
    private bool           _disposed;
    private Thread?        _thread;

    // ── Public ───────────────────────────────────────────────────────────────

    /// <summary>Raised on the monitor thread when the foreground process changes.</summary>
    public event EventHandler<string>? ForegroundAppChanged;

    /// <summary>The process name of the current foreground application.</summary>
    public string CurrentApp { get; private set; } = string.Empty;

    /// <summary>
    /// Installs the hook on a dedicated STA background thread and starts pumping messages.
    /// </summary>
    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(ThreadProc) { IsBackground = true, Name = "ForegroundMonitor" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void ThreadProc()
    {
        _procRef = OnWinEvent;
        _hook    = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            0, _procRef, 0, 0, WINEVENT_OUTOFCONTEXT);

        // Fire immediately for whichever app is already in front
        var initial = GetProcessName(GetForegroundWindow());
        if (!string.IsNullOrEmpty(initial))
            Raise(initial);

        // Message pump — required for WINEVENT_OUTOFCONTEXT hooks
        System.Windows.Forms.Application.Run();
    }

    private void OnWinEvent(
        nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        var name = GetProcessName(hwnd);
        if (!string.IsNullOrEmpty(name))
            Raise(name);
    }

    private void Raise(string name)
    {
        if (name.Equals(_lastApp, StringComparison.OrdinalIgnoreCase)) return;
        _lastApp   = name;
        CurrentApp = name;
        ForegroundAppChanged?.Invoke(this, name);
    }

    private static string GetProcessName(nint hwnd)
    {
        if (hwnd == 0) return string.Empty;
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != 0) { UnhookWinEvent(_hook); _hook = 0; }
        System.Windows.Forms.Application.ExitThread();
    }
}
