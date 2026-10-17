namespace OpenNDOF.Core.Devices;

/// <summary>A supported 6-DOF input device.</summary>
public sealed record SupportedDevice(
    int    VendorId,
    int    ProductId,
    string FriendlyName,
    DeviceType Type,
    // TDxInput Device.Type value used by COM clients, per the 3Dconnexion kd
    // device codes (SpaceNavigator=6, SpaceExplorer=4, SpaceTraveler=25,
    // SpacePilot=29, SpaceBall 5000=2, unknown=0).
    int    TypeCode,
    // Number of programmable keys the device reports (TDxInput
    // Keyboard.ProgrammableKeys). Used before any button report arrives; live
    // reports refine it to the actual maximum key index.
    int    ProgrammableKeys);

public enum DeviceType
{
    Unknown,
    SpaceNavigator,
    SpaceExplorer,
    SpacePilot,
    SpaceTraveler,
    SpaceBall,
    Aerion
}

/// <summary>Catalogue of every device OpenNDOF supports.</summary>
public static class KnownDevices
{
    public static IReadOnlyList<SupportedDevice> All { get; } =
    [
        new(0x046D, 0xC626, "SpaceNavigator", DeviceType.SpaceNavigator, 6, 2),
        new(0x046D, 0xC627, "SpaceExplorer",  DeviceType.SpaceExplorer,  4, 8),
        new(0x046D, 0xC625, "SpacePilot",     DeviceType.SpacePilot,    29, 6),
        new(0x046D, 0xC623, "SpaceTraveler",  DeviceType.SpaceTraveler, 25, 2),
        new(0x046D, 0xC621, "SpaceBall 5000",     DeviceType.SpaceBall,  2, 8),
        new(0x046D, 0xC622, "SpaceBall 5000 USB", DeviceType.SpaceBall,  2, 8),
        new(0x03EB, 0x2013, "Aerion NDOF",     DeviceType.Aerion,         0, 0),
    ];

    public static SupportedDevice? Match(int vendorId, int productId) =>
        All.FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId);
}
