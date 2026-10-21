using OpenNDOF.Core.Com;

namespace OpenNDOF.IntegrationTests;

/// <summary>
/// Exercises the REAL activation path (ComServer singleton → hid.dll → no
/// device found) and asserts the trace captures each step — the exact
/// diagnostic a silent client like SolidWorks produces: an empty trace means
/// the client never activated the server.
/// </summary>
public sealed class ComActivationTraceTests : IDisposable
{
    private readonly string _path;
    private readonly string _originalPath;

    public ComActivationTraceTests()
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
    public void Connect_RunsRealActivationPath_AndTracesEachStep()
    {
        var device = new Device();

        device.Connect();    // no hardware on CI → stays disconnected, but every step traces
        device.Disconnect();

        var text = File.ReadAllText(_path);
        Assert.Contains("Device created", text);
        Assert.Contains("Device.Connect() called", text);
        Assert.Contains("ComServer.GetDevice(): first call", text);
        Assert.Contains("Device.Disconnect() called", text);
    }
}