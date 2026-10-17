using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Input;
using OpenNDOF.Core.Profiles;
using OpenNDOF.HID;

namespace OpenNDOF.UnitTests;

public class SpaceDeviceTests : IDisposable
{
    private readonly string _profilePath;
    private readonly ProfileManager _profiles;
    private readonly FakeHidController _hid;
    private readonly SpaceDevice _device;
    private readonly List<SensorState> _sensorEvents = [];
    private readonly List<KeyboardState> _keyboardEvents = [];
    private readonly List<int> _buttonEvents = [];

    public SpaceDeviceTests()
    {
        _profilePath = Path.Combine(Path.GetTempPath(), $"opendndof-space-{Guid.NewGuid():N}.json");
        _profiles = new ProfileManager(_profilePath);
        _hid = new FakeHidController();
        _device = new SpaceDevice(_hid, _profiles);
        _device.SensorUpdated += (_, s) => _sensorEvents.Add(s);
        _device.KeyboardUpdated += (_, k) => _keyboardEvents.Add(k);
        _device.ButtonPressed += (_, b) => _buttonEvents.Add(b);
    }

    public void Dispose()
    {
        _device.Dispose();
        File.Delete(_profilePath);
    }

    private FakeHidDevice AddNavigator(string path = "nav")
    {
        var nav = new FakeHidDevice { VendorId = 0x046D, ProductId = 0xC626, DevicePath = path };
        _hid.Add(nav);
        return nav;
    }

    private FakeHidDevice AddSpacePilot()
    {
        var pilot = new FakeHidDevice
        {
            VendorId = 0x046D, ProductId = 0xC625,
            FeatureReportLength = 8, DevicePath = "pilot"
        };
        _hid.Add(pilot);
        return pilot;
    }

    [Fact]
    public void Connect_WithSupportedDevice_ConnectsAndStartsReading()
    {
        var nav = AddNavigator();

        bool ok = _device.Connect("cad");

        Assert.True(ok);
        Assert.True(_device.IsConnected);
        Assert.Equal(DeviceType.SpaceNavigator, _device.DeviceInfo!.Type);
        Assert.Equal("SpaceNavigator", _device.DeviceInfo.FriendlyName);
        Assert.Equal("cad", _device.ActiveProfile);
        Assert.True(nav.Started);
    }

    [Fact]
    public void Connect_WithNoDevices_ReturnsFalse()
    {
        Assert.False(_device.Connect());
        Assert.False(_device.IsConnected);
        Assert.Null(_device.DeviceInfo);
        Assert.Equal("default", _device.ActiveProfile);
    }

    [Fact]
    public void Connect_WithUnsupportedDevice_ReturnsFalse()
    {
        _hid.Add(new FakeHidDevice { VendorId = 0x1234, ProductId = 0x5678, DevicePath = "x" });

        Assert.False(_device.Connect());
        Assert.False(_device.IsConnected);
    }

    [Fact]
    public void Connect_WithSpacePilot_SetsUpLcdDisplay()
    {
        var pilot = AddSpacePilot();

        Assert.True(_device.Connect());
        Assert.True(_device.IsConnected);
        Assert.NotNull(_device.Overlay);
    }

    [Fact]
    public void TranslationReport_UpdatesSensor()
    {
        var nav = AddNavigator();
        _device.Connect();

        nav.InjectReport([0x01, 0x5E, 0x01, 0x00, 0x00, 0x00, 0x00]);

        var s = Assert.Single(_sensorEvents);
        Assert.Equal(1.0, s.Tx);
        Assert.Equal(0.0, s.Ty);
        Assert.Equal(0.0, s.Tz);
    }

    [Fact]
    public void TranslationAndRotationReports_MergeIntoOneState()
    {
        var nav = AddNavigator();
        _device.Connect();

        nav.InjectReport([0x01, 0x5E, 0x01, 0x00, 0x00, 0x00, 0x00]);
        nav.InjectReport([0x02, 0xA2, 0xFE, 0x00, 0x00, 0x00, 0x00]);

        Assert.Equal(2, _sensorEvents.Count);
        var last = _sensorEvents[^1];
        Assert.Equal(1.0, last.Tx);
        Assert.Equal(-1.0, last.Rx);
    }

    [Fact]
    public void ButtonReport_RaisesPressesOnlyOnLeadingEdge()
    {
        var nav = AddNavigator();
        _device.Connect();

        nav.InjectReport([0x03, 0x01, 0x00]);      // button 0 down
        nav.InjectReport([0x03, 0x03, 0x00]);      // button 1 also down
        nav.InjectReport([0x03, 0x03, 0x00]);      // unchanged — nothing
        nav.InjectReport([0x03, 0x00, 0x00]);      // all released

        Assert.Equal([0, 1], _buttonEvents);
        Assert.Equal(3, _keyboardEvents.Count);
        var second = _keyboardEvents[1];
        Assert.True(second.IsPressed(0) && second.IsPressed(1));
        Assert.Empty(_keyboardEvents[^1].PressedKeys);
    }

    [Fact]
    public void ButtonReport_AcrossMultipleBytes_DecodesHighIndices()
    {
        var nav = AddNavigator();
        _device.Connect();

        nav.InjectReport([0x03, 0x00, 0x01, 0x00]); // button 8

        Assert.Equal([8], _buttonEvents);
        Assert.True(_keyboardEvents[^1].IsPressed(8));
    }

    [Fact]
    public void ShortReports_AreIgnored()
    {
        var nav = AddNavigator();
        _device.Connect();

        nav.InjectReport([]);                          // no report id
        nav.InjectReport([0x01, 0x5E]);                // translation truncated
        nav.InjectReport([0x03]);                      // button report, no payload

        Assert.Empty(_sensorEvents);
        Assert.Empty(_keyboardEvents);
        Assert.Empty(_buttonEvents);
    }

    [Fact]
    public void Disconnect_StopsReadingAndClearsState()
    {
        var nav = AddNavigator();
        _device.Connect();

        _device.Disconnect();

        Assert.False(_device.IsConnected);
        Assert.Null(_device.DeviceInfo);
        Assert.False(nav.Started);
        Assert.Null(_device.Overlay);
    }

    [Fact]
    public void DevicesChanged_ConnectsWhenDeviceAppears()
    {
        Assert.False(_device.IsConnected);

        AddNavigator("plugged");
        _hid.RaiseDevicesChanged();

        Assert.True(_device.IsConnected);
    }

    [Fact]
    public void WriteDisplayLines_ReturnsFalseForNonSpacePilot()
    {
        AddNavigator();
        _device.Connect();

        Assert.False(_device.WriteDisplayLines("hello"));
        Assert.False(_device.WriteButtonGrid("app", new string[6]));
    }

    [Fact]
    public void WriteButtonGrid_OnSpacePilot_WritesFeatureReports()
    {
        var pilot = AddSpacePilot();
        _device.Connect();
        int baseline = pilot.FeatureReports.Count;

        bool ok = _device.WriteButtonGrid("TestApp", ["A", "B", "C", "D", "E", "F"]);

        Assert.True(ok);
        Assert.True(pilot.FeatureReports.Count > baseline);
    }

    [Fact]
    public void WriteDisplayLines_OnSpacePilot_WritesFeatureReports()
    {
        var pilot = AddSpacePilot();
        _device.Connect();
        int baseline = pilot.FeatureReports.Count;

        bool ok = _device.WriteDisplayLines("Hello", "World");

        Assert.True(ok);
        Assert.True(pilot.FeatureReports.Count > baseline);
    }
}