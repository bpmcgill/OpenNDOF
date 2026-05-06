namespace OpenNDOF.OtherTests;

using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Input;

public class CoreModelBehaviorTests
{
    [Fact]
    public void SensorState_WithScale_ScalesEachAxis()
    {
        var state = new SensorState(1, -2, 3, -4, 5, -6);

        var scaled = state.WithScale(2, 0.5, 1, 0.25, 2, -1);

        Assert.Equal(2, scaled.Tx);
        Assert.Equal(-1, scaled.Ty);
        Assert.Equal(3, scaled.Tz);
        Assert.Equal(-1, scaled.Rx);
        Assert.Equal(10, scaled.Ry);
        Assert.Equal(6, scaled.Rz);
    }

    [Fact]
    public void SensorState_IsZero_TrueOnlyForNearZeroValues()
    {
        Assert.True(SensorState.Zero.IsZero());
        Assert.False(new SensorState(0.01, 0, 0, 0, 0, 0).IsZero());
    }

    [Fact]
    public void KeyboardState_TracksPressedKeys()
    {
        var keyboard = new KeyboardState([1, 3, 5]);

        Assert.True(keyboard.IsPressed(3));
        Assert.False(keyboard.IsPressed(2));
        Assert.Equal(3, keyboard.PressedKeys.Count);
    }

    [Fact]
    public void KnownDevices_Match_ReturnsExpectedDevice()
    {
        var device = KnownDevices.Match(0x046D, 0xC625);

        Assert.NotNull(device);
        Assert.Equal(DeviceType.SpacePilot, device!.Type);
        Assert.Equal("SpacePilot", device.FriendlyName);
    }
}
