using System.Runtime.InteropServices;

namespace OpenNDOF.Core.Com;

// ── Value types ───────────────────────────────────────────────────────────────

[ComVisible(true)]
[Guid("8C2AA71D-2B23-43F5-A6ED-4DF57E9CD8D5")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IVector3D
{
    double X      { get; set; }
    double Y      { get; set; }
    double Z      { get; set; }
    double Length { get; set; }
}

[ComVisible(true)]
[Guid("1EF2BAFF-54E9-4706-9F61-078F7134FD35")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IAngleAxis
{
    double X     { get; set; }
    double Y     { get; set; }
    double Z     { get; set; }
    double Angle { get; set; }
}

// ── Sensor ────────────────────────────────────────────────────────────────────

[ComVisible(true)]
[Guid("E6929A4A-6F41-46C6-9252-A8CC53472CB1")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface _ISensorEvents
{
    [DispId(1)] void SensorInput();
}

[ComVisible(true)]
[Guid("F3A6775E-6FA1-4829-BF32-5B045C29078F")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface ISensor
{
    IVector3D  Translation { get; }
    IAngleAxis Rotation    { get; }
    object     Device      { get; }
    double     Period      { get; }
}

// ── Keyboard ──────────────────────────────────────────────────────────────────

[ComVisible(true)]
[Guid("6B6BB0A8-4491-40CF-B1A9-C15A801FE151")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface _IKeyboardEvents
{
    [DispId(1)] void KeyDown(int keyCode);
    [DispId(2)] void KeyUp(int keyCode);
}

[ComVisible(true)]
[Guid("D6F968E7-2993-48D7-AF24-8B602D925B2C")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IKeyboard
{
    int    Keys             { get; }
    int    ProgrammableKeys { get; }
    string GetKeyLabel(int keyCode);
    string GetKeyName(int keyCode);
    object Device           { get; }
    bool   IsKeyDown(int keyCode);
    bool   IsKeyUp(int keyCode);
}

// ── Device ────────────────────────────────────────────────────────────────────

[ComVisible(true)]
[Guid("8FE3A216-E235-49A6-9136-F9D81FDADEF5")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface _ISimpleDeviceEvents
{
    [DispId(1)] void DeviceChange(int reserved);
}

[ComVisible(true)]
[Guid("CB3BF65E-0816-482A-BB11-64AF1E837812")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface ISimpleDevice
{
    void    Connect();
    void    Disconnect();
    Sensor   Sensor      { get; }
    Keyboard Keyboard    { get; }
    int      Type        { get; }
    bool     IsConnected { get; }
    void     LoadPreferences(string registryPath);
}

// ── TDxInfo ───────────────────────────────────────────────────────────────────

[ComVisible(true)]
[Guid("00612962-8FB6-47B2-BF98-4E8C0FF5F559")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface ITDxInfo
{
    string RevisionNumber();
}
