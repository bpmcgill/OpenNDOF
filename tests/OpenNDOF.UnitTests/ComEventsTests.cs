using System.Runtime.InteropServices;
using OpenNDOF.Core.Com;
using OpenNDOF.Core.Input;

namespace OpenNDOF.UnitTests;

// Native-style event sinks: their CCWs act as the "client" that subscribes
// through IConnectionPointContainer, exactly like SolidWorks etc. do.
[ComVisible(true)]
public sealed class FakeSensorSink : _ISensorEvents
{
    public int SensorInputCount;
    public void SensorInput() => SensorInputCount++;
}

[ComVisible(true)]
public sealed class FakeKeyboardSink : _IKeyboardEvents
{
    public readonly List<int> Down = [];
    public readonly List<int> Up = [];
    public void KeyDown(int keyCode) => Down.Add(keyCode);
    public void KeyUp(int keyCode) => Up.Add(keyCode);
}

[ComVisible(true)]
public sealed class FakeDeviceSink : _ISimpleDeviceEvents
{
    public readonly List<int> Changes = [];
    public void DeviceChange(int reserved) => Changes.Add(reserved);
}

[ComVisible(true)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INoDispatch { }

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class NoDispatchSink : INoDispatch { }

/// <summary>
/// Verifies that OpenNDOF's COM-visible classes expose connection points and
/// deliver events to a subscribed sink (the previously-missing capability that
/// native TDxInput clients rely on).
/// </summary>
public class ComEventsTests
{
    private static nint SinkPointer(object sink, Type eventInterface)
        => Marshal.GetComInterfaceForObject(sink, eventInterface);

    private static (IConnectionPointContainer Cpc, IntPtr Ptr) GetContainer(object obj, Type iface)
    {
        nint pUnk = Marshal.GetComInterfaceForObject(obj, iface);
        int hr = Marshal.QueryInterface(pUnk, in ComIids.IConnectionPointContainer, out nint pCpc);
        Marshal.Release(pUnk);
        Assert.Equal(0, hr); // CCW must now expose IConnectionPointContainer
        return ((IConnectionPointContainer)Marshal.GetTypedObjectForIUnknown(
            pCpc, typeof(IConnectionPointContainer)), pCpc);
    }

    private static IConnectionPoint FindPoint(IConnectionPointContainer cpc, Guid eventIid)
    {
        Guid riid = eventIid;
        cpc.FindConnectionPoint(ref riid, out nint pCp);
        return (IConnectionPoint)Marshal.GetTypedObjectForIUnknown(pCp, typeof(IConnectionPoint));
    }

    [Fact]
    public void Sensor_ExposesConnectionPoint_AndDeliversSensorInput()
    {
        var sensor = new Sensor();
        var sink = new FakeSensorSink();
        nint pSink = SinkPointer(sink, typeof(_ISensorEvents));
        var (cpc, pCpc) = GetContainer(sensor, typeof(ISensor));
        try
        {
            var point = FindPoint(cpc, ComIids.SensorEvents);
            point.Advise(pSink, out int cookie);
            Assert.True(cookie > 0);

            sensor.Update(new SensorState(1, 0, 0, 0, 0, 0), 0.016, new object());
            sensor.Update(new SensorState(2, 0, 0, 0, 0, 0), 0.016, new object());

            Assert.Equal(2, sink.SensorInputCount);

            point.Unadvise(cookie);
            sensor.Update(new SensorState(3, 0, 0, 0, 0, 0), 0.016, new object());
            Assert.Equal(2, sink.SensorInputCount); // no delivery after unadvise
        }
        finally
        {
            Marshal.Release(pSink);
            Marshal.Release(pCpc);
        }
    }

    [Fact]
    public void Keyboard_ExposesConnectionPoint_AndDeliversKeyDownKeyUp()
    {
        var keyboard = new Keyboard();
        var sink = new FakeKeyboardSink();
        nint pSink = SinkPointer(sink, typeof(_IKeyboardEvents));
        var (cpc, pCpc) = GetContainer(keyboard, typeof(IKeyboard));
        try
        {
            var point = FindPoint(cpc, ComIids.KeyboardEvents);
            point.Advise(pSink, out _);

            keyboard.Update([1, 2], new object());
            keyboard.Update([2], new object());

            Assert.Equal([1, 2], sink.Down);
            Assert.Equal([1], sink.Up);
        }
        finally
        {
            Marshal.Release(pSink);
            Marshal.Release(pCpc);
        }
    }

    [Fact]
    public void Device_ExposesConnectionPoint_ForDeviceChange()
    {
        var device = new Device();
        nint pSink = SinkPointer(new FakeDeviceSink(), typeof(_ISimpleDeviceEvents));
        var (cpc, pCpc) = GetContainer(device, typeof(ISimpleDevice));
        try
        {
            var point = FindPoint(cpc, ComIids.SimpleDeviceEvents);
            Assert.NotNull(point);
        }
        finally
        {
            Marshal.Release(pSink);
            Marshal.Release(pCpc);
        }
    }

    [Fact]
    public void ConnectionPointContainer_RaisesToSink_WithArguments()
    {
        var sink = new FakeDeviceSink();
        nint pSink = SinkPointer(sink, typeof(_ISimpleDeviceEvents));
        try
        {
            using var container = new ConnectionPointContainer(ComIids.SimpleDeviceEvents, (1, "DeviceChange"));
            Guid iid = ComIids.SimpleDeviceEvents;
            container.FindConnectionPoint(ref iid, out nint pCp);
            var point = (IConnectionPoint)Marshal.GetTypedObjectForIUnknown(pCp, typeof(IConnectionPoint));
            point.Advise(pSink, out _);

            container.Raise(1, 7);
            container.Raise(1, 9);

            Assert.Equal([7, 9], sink.Changes);
        }
        finally
        {
            Marshal.Release(pSink);
        }
    }

    [Fact]
    public void ConnectionPointContainer_FindConnectionPoint_WrongIid_ThrowsNoInterface()
    {
        using var container = new ConnectionPointContainer(ComIids.SensorEvents, (1, "SensorInput"));
        Guid wrong = Guid.NewGuid();

        var ex = Assert.Throws<COMException>(() => container.FindConnectionPoint(ref wrong, out _));

        Assert.Equal(unchecked((int)0x80004002), ex.HResult); // E_NOINTERFACE
    }

    [Fact]
    public void Advise_WithNonDispatchableSink_Throws()
    {
        // NoDispatchSink exposes no IDispatch — Advise must reject it like a
        // real COM server would when the sink is not an IDispatch sink.
        var container = new ConnectionPointContainer(ComIids.SensorEvents, (1, "SensorInput"));
        Guid iid = ComIids.SensorEvents;
        container.FindConnectionPoint(ref iid, out nint pCp);
        var point = (IConnectionPoint)Marshal.GetTypedObjectForIUnknown(pCp, typeof(IConnectionPoint));

        nint pBad = Marshal.GetComInterfaceForObject(new NoDispatchSink(), typeof(INoDispatch));
        try
        {
            Assert.ThrowsAny<COMException>(() => point.Advise(pBad, out _));
        }
        finally
        {
            Marshal.Release(pBad);
            Marshal.Release(pCp);
            container.Dispose();
        }
    }
}