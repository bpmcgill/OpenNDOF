using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Profiles;

namespace OpenNDOF.UnitTests;

public class MacroExecutorTests : IDisposable
{
    private readonly string _profilePath;
    private readonly ProfileManager _profiles;
    private readonly FakeHidController _hid;
    private readonly SpaceDevice _device;

    public MacroExecutorTests()
    {
        _profilePath = Path.Combine(Path.GetTempPath(), $"opendndof-macro-{Guid.NewGuid():N}.json");
        _profiles = new ProfileManager(_profilePath);
        _hid = new FakeHidController();
        _device = new SpaceDevice(_hid, _profiles);
    }

    public void Dispose()
    {
        _device.Dispose();
        File.Delete(_profilePath);
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("^z", "{^}z")]
    [InlineData("a+b", "a{+}b")]
    [InlineData("100%", "100{%}")]
    [InlineData("({[", "{(}{{}{[}")]
    [InlineData("~x", "{~}x")]
    [InlineData("", "")]
    public void EscapeSendKeys_EscapesSpecialCharacters(string input, string expected)
        => Assert.Equal(expected, MacroExecutor.EscapeSendKeys(input));

    [Fact]
    public void Attach_Detach_AreIdempotent()
    {
        using var executor = new MacroExecutor(_device, _profiles);

        executor.Attach();
        executor.Attach();
        executor.Detach();
        executor.Detach();
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var executor = new MacroExecutor(_device, _profiles);
        executor.Attach();
        executor.Dispose();
        executor.Dispose(); // second dispose is a no-op
    }

    [Fact]
    public void ButtonPress_WithNoAction_DoesNotExecute()
    {
        using var executor = new MacroExecutor(_device, _profiles);
        executor.Attach();
        var nav = new FakeHidDevice { VendorId = 0x046D, ProductId = 0xC626, DevicePath = "m" };
        _hid.Add(nav);
        var presses = new List<int>();
        _device.ButtonPressed += (_, b) => presses.Add(b);
        Assert.True(_device.Connect());

        // Default profile binds MacroType.None to every button — events still surface,
        // but no SendKeys/keystrokes are produced.
        nav.InjectReport([0x03, 0x01, 0x00]);

        Assert.Equal([0], presses);
    }

    [Fact]
    public void SetCurrentApp_ResolvesMatchingProfile_WithoutThrowing()
    {
        _profiles.AddOrUpdate(new DeviceProfile { Name = "cad", AppNames = ["acad"] });
        using var executor = new MacroExecutor(_device, _profiles);

        executor.SetCurrentApp("ACAD");
    }
}