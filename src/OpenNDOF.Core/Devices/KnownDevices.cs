namespace OpenNDOF.Core.Devices;

/// <summary>A supported 6-DOF input device.</summary>
public sealed record SupportedDevice(
    int    VendorId,
    int    ProductId,
    string FriendlyName,
    DeviceType Type);

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
        new(0x046D, 0xC626, "SpaceNavigator",  DeviceType.SpaceNavigator),
        new(0x046D, 0xC627, "SpaceExplorer",   DeviceType.SpaceExplorer),
        new(0x046D, 0xC625, "SpacePilot",      DeviceType.SpacePilot),
        new(0x046D, 0xC623, "SpaceTraveler",   DeviceType.SpaceTraveler),
        new(0x046D, 0xC621, "SpaceBall 5000",  DeviceType.SpaceBall),
        new(0x046D, 0xC622, "SpaceBall 5000 USB", DeviceType.SpaceBall),
        new(0x03EB, 0x2013, "Aerion NDOF",     DeviceType.Aerion),
    ];

    public static SupportedDevice? Match(int vendorId, int productId) =>
        All.FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId);
}
