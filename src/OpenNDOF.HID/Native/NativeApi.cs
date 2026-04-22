using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpenNDOF.HID.Native;

internal static partial class NativeApi
{
    // ── File I/O ────────────────────────────────────────────────────────────
    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW",
        SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        nint lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, nint hTemplateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CancelIo(SafeFileHandle hFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer,
        uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, nint lpOverlapped);

    // IOCTL_HID_SET_FEATURE = CTL_CODE(FILE_DEVICE_KEYBOARD=0x0B, 0x103, METHOD_IN_DIRECT=1, FILE_ANY_ACCESS=0)
    internal const uint IOCTL_HID_SET_FEATURE = 0x000B040D;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        byte[] lpInBuffer, uint nInBufferSize,
        nint lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, nint lpOverlapped);

    // ── HID ─────────────────────────────────────────────────────────────────
    [LibraryImport("hid.dll")]
    internal static partial void HidD_GetHidGuid(ref Guid hidGuid);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool HidD_GetAttributes(SafeFileHandle hDevice, ref HiddAttributes attributes);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool HidD_GetPreparsedData(SafeFileHandle hDevice, out nint preparsedData);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool HidD_FreePreparsedData(nint preparsedData);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool HidD_SetOutputReport(SafeFileHandle hDevice,
        byte[] reportBuffer, uint reportBufferLength);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool HidD_SetFeature(SafeFileHandle hDevice,
        byte[] reportBuffer, uint reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern int HidP_GetCaps(nint preparsedData, ref HidpCaps capabilities);

    // ── SetupApi ─────────────────────────────────────────────────────────────
    internal const uint DIGCF_DEVICEINTERFACE = 0x10;
    internal const uint DIGCF_PRESENT         = 0x02;

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern nint SetupDiGetClassDevs(
        ref Guid classGuid, nint enumerator, nint hwndParent, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInterfaces(
        nint deviceInfoSet, nint deviceInfoData, ref Guid interfaceClassGuid,
        uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(
        nint deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData,
        nint deviceInterfaceDetailData, int deviceInterfaceDetailDataSize,
        ref int requiredSize, nint deviceInfoData);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);

    // ── User32 (device notifications) ────────────────────────────────────────
    internal const int WM_DEVICECHANGE             = 0x0219;
    internal const int DBT_DEVICEARRIVAL           = 0x8000;
    internal const int DBT_DEVICEREMOVECOMPLETE     = 0x8004;
    internal const int DBT_DEVTYP_DEVICEINTERFACE  = 0x05;
    internal const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00;

    [LibraryImport("user32.dll", EntryPoint = "RegisterDeviceNotificationW", SetLastError = true)]
    internal static partial nint RegisterDeviceNotification(
        nint recipient, nint notificationFilter, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterDeviceNotification(nint handle);

    // ── Structures ───────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    internal struct HiddAttributes
    {
        public int    Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpDeviceInterfaceData
    {
        public int    cbSize;
        public Guid   InterfaceClassGuid;
        public uint   Flags;
        public nint   Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DevBroadcastDeviceInterface
    {
        public int  dbcc_size;
        public int  dbcc_devicetype;
        public int  dbcc_reserved;
        public Guid dbcc_classguid;
        public char dbcc_name;
    }
}
