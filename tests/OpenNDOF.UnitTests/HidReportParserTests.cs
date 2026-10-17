using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Input;
using OpenNDOF.Core.Profiles;

namespace OpenNDOF.UnitTests;

public class HidReportParserTests
{
    private static readonly DeviceProfile Default = new();

    /// <summary>Build a report with the given little-endian values at the mapped axis offsets.</summary>
    private static byte[] Report(byte id, int t1, int t2, int t3)
    {
        byte[] r = [id, 0, 0, 0, 0, 0, 0];
        foreach ((int value, int offset) in new[] { (t1, 1), (t2, 3), (t3, 5) })
        {
            short raw = checked((short)value);
            r[offset]     = (byte)(raw & 0xFF);
            r[offset + 1] = (byte)(raw >> 8);
        }
        return r;
    }

    [Fact]
    public void ToAxis_ReadsSignedLittleEndian()
    {
        Assert.Equal(0.0, HidReportParser.ToAxis(Report(0x01, 0, 0, 0), 1));
        Assert.Equal(1.0, HidReportParser.ToAxis(Report(0x01, 350, 0, 0), 1));
        Assert.Equal(-1.0, HidReportParser.ToAxis(Report(0x01, -350, 0, 0), 1));
        Assert.Equal(0.5, HidReportParser.ToAxis(Report(0x01, 175, 0, 0), 1));
    }

    [Fact]
    public void ParseTranslation_MapsAxesAndAppliesScale()
    {
        var profile = new DeviceProfile { ScaleTx = 2, ScaleTy = 3, ScaleTz = 4 };

        // tx=+350 (+1.0), ty=+350 (Ty is negated: −1.0), tz=−350 (−1.0)
        var s = HidReportParser.ParseTranslation(Report(0x01, 350, -350, 350), profile, SensorState.Zero);

        Assert.Equal(2.0, s.Tx);
        Assert.Equal(-3.0, s.Ty);
        Assert.Equal(-4.0, s.Tz);
        Assert.Equal(0.0, s.Rx);
        Assert.Equal(0.0, s.Ry);
        Assert.Equal(0.0, s.Rz);
    }

    [Fact]
    public void ParseTranslation_PreservesPendingRotation()
    {
        var pending = new SensorState(0, 0, 0, 1, 2, 3);

        var s = HidReportParser.ParseTranslation(Report(0x01, 0, 0, 0), Default, pending);

        Assert.Equal(0.0, s.Tx);
        Assert.Equal(1.0, s.Rx);
        Assert.Equal(2.0, s.Ry);
        Assert.Equal(3.0, s.Rz);
    }

    [Fact]
    public void ParseRotation_MapsAxesAndPreservesTranslation()
    {
        var profile = new DeviceProfile { ScaleRx = 0.5, ScaleRy = 1, ScaleRz = 2 };
        var pending = new SensorState(1, 2, 3, 0, 0, 0);

        // rx=−350 (−1.0 × 0.5), ry=−350 (−1.0), rz=+350 (+1.0 × 2)
        var s = HidReportParser.ParseRotation(Report(0x02, -350, -350, 350), profile, pending);

        Assert.Equal(-0.5, s.Rx);
        Assert.Equal(-1.0, s.Ry);
        Assert.Equal(2.0, s.Rz);
        Assert.Equal(1.0, s.Tx);
        Assert.Equal(2.0, s.Ty);
        Assert.Equal(3.0, s.Tz);
    }

    [Fact]
    public void ParseRotation_ZeroProfileScales_ProducesZeroRotation()
    {
        var profile = new DeviceProfile { ScaleRx = 0, ScaleRy = 0, ScaleRz = 0 };

        var s = HidReportParser.ParseRotation(Report(0x02, 350, 350, 350), profile, SensorState.Zero);

        Assert.Equal(0.0, s.Rx);
        Assert.Equal(0.0, s.Ry);
        Assert.Equal(0.0, s.Rz);
    }

    [Fact]
    public void ParseButtons_DecodesBitfieldAcrossBytes()
    {
        // 0x05 = bits 0,2 ; 0x81 = bits 8,15
        var pressed = HidReportParser.ParseButtons([0x03, 0x05, 0x81]);

        Assert.Equal(new HashSet<int> { 0, 2, 8, 15 }, pressed);
    }

    [Fact]
    public void ParseButtons_EmptyReport_EmptySet()
    {
        Assert.Empty(HidReportParser.ParseButtons([0x03]));
    }

    [Fact]
    public void ParseButtons_ZeroBytes_EmptySet()
    {
        Assert.Empty(HidReportParser.ParseButtons([0x03, 0x00, 0x00]));
    }
}