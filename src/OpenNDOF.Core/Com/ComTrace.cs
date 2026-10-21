namespace OpenNDOF.Core.Com;

/// <summary>
/// Best-effort activation trace for the COM server. Writes a few lines per
/// session to %TEMP%\OpenNDOF-trace.log so a failing client (e.g. SolidWorks)
/// can be checked: if the file is empty after the app runs, the client never
/// activated OpenNDOF's COM object. Tracing must never break COM, so all
/// failures are swallowed.
/// </summary>
internal static class ComTrace
{
    private static readonly string _path = Path.Combine(Path.GetTempPath(), "OpenNDOF-trace.log");
    private static readonly object _lock = new();
    private static int _sensorEvents;

    internal static void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_path,
                    $"{DateTime.UtcNow:u} [pid {Environment.ProcessId} thr {Thread.CurrentThread.ManagedThreadId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Never let tracing break a COM activation.
        }
    }

    /// <summary>Logs the first sensor event so a session can be proven live.</summary>
    internal static void SensorEvent()
    {
        if (Interlocked.Increment(ref _sensorEvents) == 1)
            Write("first SensorUpdated event delivered (device data flowing)");
    }

    internal static void ResetSensorCounter() => Interlocked.Exchange(ref _sensorEvents, 0);
}