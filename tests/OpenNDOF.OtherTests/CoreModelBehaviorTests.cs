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

    [Fact]
    public void KnownDevices_All_ContainsEverySupportedDevice()
    {
        var ids = KnownDevices.All.Select(d => $"{d.VendorId:X4}:{d.ProductId:X4}").ToHashSet();

        Assert.Equal(7, KnownDevices.All.Count);
        Assert.Contains("046D:C626", ids);   // SpaceNavigator
        Assert.Contains("046D:C627", ids);   // SpaceExplorer
        Assert.Contains("046D:C625", ids);   // SpacePilot
        Assert.Contains("046D:C623", ids);   // SpaceTraveler
        Assert.Contains("046D:C621", ids);   // SpaceBall 5000
        Assert.Contains("046D:C622", ids);   // SpaceBall 5000 USB
        Assert.Contains("03EB:2013", ids);   // Aerion NDOF
    }

    [Fact]
    public void KnownDevices_TypeCodes_MatchTdxInput()
    {
        // TDxInput Device.Type per the 3Dconnexion kd device codes.
        Assert.Equal(6,  KnownDevices.Match(0x046D, 0xC626)!.TypeCode); // SpaceNavigator
        Assert.Equal(4,  KnownDevices.Match(0x046D, 0xC627)!.TypeCode); // SpaceExplorer
        Assert.Equal(29, KnownDevices.Match(0x046D, 0xC625)!.TypeCode); // SpacePilot
        Assert.Equal(25, KnownDevices.Match(0x046D, 0xC623)!.TypeCode); // SpaceTraveler
        Assert.Equal(2,  KnownDevices.Match(0x046D, 0xC621)!.TypeCode); // SpaceBall 5000
        Assert.Equal(2,  KnownDevices.Match(0x046D, 0xC622)!.TypeCode); // SpaceBall 5000 USB
        Assert.Equal(0,  KnownDevices.Match(0x03EB, 0x2013)!.TypeCode); // Aerion — no official code
    }

    [Fact]
    public void KnownDevices_Match_UnknownVidPid_ReturnsNull()
    {
        Assert.Null(KnownDevices.Match(0x0000, 0x0000));
        Assert.Null(KnownDevices.Match(0x046D, 0x1234));
    }

    [Fact]
    public void SensorState_IsZero_UsesThresholdBoundary()
    {
        Assert.True(new SensorState(0.0009, 0, 0, 0, 0, 0).IsZero());
        Assert.False(new SensorState(0.001, 0, 0, 0, 0, 0).IsZero());
        Assert.True(new SensorState(0, 0, 0, 0, 0, -0.0009).IsZero());
        Assert.False(new SensorState(0, 0, 0, 0, 0, -0.001).IsZero());
    }

    [Fact]
    public void SensorState_ToString_PresentsAllAxes()
    {
        string text = new SensorState(1, -2, 0.5, 3, -4, 0.25).ToString();

        Assert.Contains("T(1.00, -2.00, 0.50)", text);
        Assert.Contains("R(3.00, -4.00, 0.25)", text);
    }

    [Fact]
    public void KeyboardState_DeduplicatesKeys()
    {
        var keyboard = new KeyboardState([1, 1, 2, 1]);

        Assert.Equal(2, keyboard.PressedKeys.Count);
        Assert.True(keyboard.IsPressed(1));
        Assert.False(keyboard.IsPressed(3));
        Assert.Empty(KeyboardState.Empty.PressedKeys);
    }
}
