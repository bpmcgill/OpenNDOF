using OpenNDOF.Core.Com;
using OpenNDOF.Core.Input;

namespace OpenNDOF.UnitTests;

internal sealed class FakeComDeviceInfo : IComDeviceInfo
{
    public int TypeCode { get; init; }
    public int ProgrammableKeys { get; init; }
}

public class SensorComTests
{
    [Fact]
    public void Update_SetsTranslationRotationPeriodAndDevice()
    {
        var sensor = new Sensor();
        var device = new object();

        sensor.Update(new SensorState(1.5, -2, 3, 30, 0, 0), 0.016, device);

        Assert.Equal(1.5, sensor.Translation.X);
        Assert.Equal(-2, sensor.Translation.Y);
        Assert.Equal(3, sensor.Translation.Z);
        Assert.Equal(30, sensor.Rotation.Angle);
        Assert.Equal(1.0, sensor.Rotation.X);
        Assert.Equal(0.0, sensor.Rotation.Y);
        Assert.Equal(0.0, sensor.Rotation.Z);
        Assert.Equal(0.016, sensor.Period);
        Assert.Same(device, sensor.Device);
    }

    [Fact]
    public void Update_RaisesSensorInputPerUpdate()
    {
        var sensor = new Sensor();
        int fired = 0;
        sensor.SensorInput += () => fired++;

        sensor.Update(SensorState.Zero, 0, new object());
        sensor.Update(new SensorState(1, 0, 0, 0, 0, 0), 0, new object());

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Update_ZeroRotation_DefaultsToYAxis()
    {
        var sensor = new Sensor();

        sensor.Update(SensorState.Zero, 0, new object());

        Assert.Equal(0.0, sensor.Rotation.Angle);
        Assert.Equal(0.0, sensor.Rotation.X);
        Assert.Equal(1.0, sensor.Rotation.Y);
        Assert.Equal(0.0, sensor.Rotation.Z);
    }

    [Fact]
    public void Update_Rotation_NormalizesToUnitAxis()
    {
        var sensor = new Sensor();

        sensor.Update(new SensorState(0, 0, 0, 30, 0, 0), 0, new object());

        Assert.Equal(30.0, sensor.Rotation.Angle);
        Assert.Equal(1.0, sensor.Rotation.X, precision: 10);
        Assert.Equal(0.0, sensor.Rotation.Y);
        Assert.Equal(0.0, sensor.Rotation.Z);
    }
}

public class KeyboardComTests
{
    [Fact]
    public void Update_FiresKeyDownAndKeyUpOnChanges()
    {
        var keyboard = new Keyboard();
        var down = new List<int>();
        var up = new List<int>();
        keyboard.KeyDown += k => down.Add(k);
        keyboard.KeyUp += k => up.Add(k);
        var device = new object();

        keyboard.Update([1, 2], device);
        keyboard.Update([2, 3], device);

        Assert.Equal([1, 2, 3], down);
        Assert.Equal([1], up);
        Assert.Equal(2, keyboard.Keys);
        Assert.True(keyboard.IsKeyDown(2));
        Assert.True(keyboard.IsKeyDown(3));
        Assert.True(keyboard.IsKeyUp(1));
        Assert.Same(device, keyboard.Device);
    }

    [Fact]
    public void Update_UnchangedKeys_RaiseNoEvents()
    {
        var keyboard = new Keyboard();
        int down = 0, up = 0;
        keyboard.KeyDown += _ => down++;
        keyboard.KeyUp += _ => up++;

        keyboard.Update([5], new object());
        keyboard.Update([5], new object());
        keyboard.Update([], new object());

        Assert.Equal(1, down);
        Assert.Equal(1, up);
        Assert.Equal(0, keyboard.Keys);
    }

    [Fact]
    public void Labels_AndCapabilities_AreStable()
    {
        var keyboard = new Keyboard();

        Assert.Equal("3", keyboard.GetKeyLabel(3));
        Assert.Equal("Key3", keyboard.GetKeyName(3));
        Assert.Equal(0, keyboard.ProgrammableKeys);
    }

    [Fact]
    public void ProgrammableKeys_GrowsWithObservedButtons()
    {
        var keyboard = new Keyboard();

        Assert.Equal(0, keyboard.ProgrammableKeys);

        keyboard.Update([0, 7], new object());

        Assert.Equal(8, keyboard.ProgrammableKeys);
    }

    [Fact]
    public void ProgrammableKeys_CombinesDeviceFallbackAndObservedButtons()
    {
        var keyboard = new Keyboard();
        var info = new FakeComDeviceInfo { TypeCode = 4, ProgrammableKeys = 8 };

        keyboard.SetDevice(info);
        Assert.Equal(8, keyboard.ProgrammableKeys);

        keyboard.Update([15], info);
        Assert.Equal(16, keyboard.ProgrammableKeys);
    }

    [Fact]
    public void Device_Type_IsZeroWhenNotConnected()
    {
        Assert.Equal(0, new Device().Type);
    }

    [Fact]
    public void Device_ExposesCapabilities_FromConnectedDevice()
    {
        var device = new Device();
        IComDeviceInfo info = device;

        Assert.Equal(0, info.TypeCode);         // no device connected
        Assert.Equal(0, info.ProgrammableKeys); // no device connected
    }
}

public class Vector3DTests
{
    [Fact]
    public void Length_IsEuclideanNorm()
    {
        var v = new Vector3D { X = 3, Y = 4, Z = 0 };
        Assert.Equal(5.0, v.Length);
    }

    [Fact]
    public void LengthSetter_ScalesPreservingDirection()
    {
        var v = new Vector3D { X = 6, Y = 8, Z = 0 };

        v.Length = 5;

        Assert.Equal(3.0, v.X, precision: 10);
        Assert.Equal(4.0, v.Y, precision: 10);
        Assert.Equal(5.0, v.Length, precision: 10);
    }

    [Fact]
    public void LengthSetter_ZeroVector_IsNoOp()
    {
        var v = new Vector3D();

        v.Length = 5;

        Assert.Equal(0.0, v.X);
        Assert.Equal(0.0, v.Y);
        Assert.Equal(0.0, v.Length);
    }
}