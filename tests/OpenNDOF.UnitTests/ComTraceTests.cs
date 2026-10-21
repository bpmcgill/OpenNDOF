using OpenNDOF.Core.Com;

namespace OpenNDOF.UnitTests;

/// <summary>
/// The activation trace is the diagnostic that tells us whether a silent
/// client (SolidWorks) ever activates the COM server. These tests verify the
/// trace itself: once-only sensor-event semantics and the activation line on
/// Device creation.
/// </summary>
public class ComTraceTests : IDisposable
{
    private readonly string _path;
    private readonly string _originalPath;

    public ComTraceTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"opendndof-trace-{Guid.NewGuid():N}.log");
        _originalPath = ComTrace.TraceFile;
        ComTrace.TraceFile = _path;
    }

    public void Dispose()
    {
        ComTrace.TraceFile = _originalPath;
        try { File.Delete(_path); } catch { /* best effort */ }
    }

    [Fact]
    public void FirstSensorEvent_IsLoggedOnce()
    {
        ComTrace.SensorEvent();
        ComTrace.SensorEvent();
        ComTrace.SensorEvent();

        var text = File.ReadAllText(_path);

        Assert.Equal(1, Count(text, "first SensorUpdated event delivered"));
    }

    [Fact]
    public void DeviceCreation_AppendsActivationLine()
    {
        var device = new Device();

        var text = File.ReadAllText(_path);

        Assert.Contains("Device created", text);
        Assert.Contains($"[pid {Environment.ProcessId}", text);
    }

    [Fact]
    public void Write_WhenFileLocked_SwallowsAndWritesNothing()
    {
        using (var stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            // AppendAllText fails against an exclusive lock — tracing must swallow.
            ComTrace.Write("locked-out");
        }

        Assert.DoesNotContain("locked-out", File.ReadAllText(_path));
    }

    private static int Count(string text, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}