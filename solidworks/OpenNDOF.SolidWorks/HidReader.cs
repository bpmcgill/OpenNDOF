using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenNDOF.SolidWorks
{
    /// <summary>
    /// Raw HID reader for 3Dconnexion devices, using the same report layout and axis
    /// convention as OpenNDOF.Core's SpaceDevice:
    ///   0x01 translation X@1 Z@3 Y@5 (Y negated), 0x02 rotation Rx@1 Ry@3 Rz@5, 0x03 buttons.
    /// A background thread reads with overlapped I/O; <see cref="Read"/> returns the latest state.
    /// Overlapped I/O matters: a device at rest sends no reports, so a blocking ReadFile
    /// would never return and closing its handle would hang SOLIDWORKS on exit.
    /// </summary>
    internal sealed class HidReader : IDisposable
    {
        // Keep in sync with src/OpenNDOF.Core/Devices/KnownDevices.cs.
        private static readonly string[] KnownDevices =
        {
            "vid_046d&pid_c626", // SpaceNavigator
            "vid_046d&pid_c627", // SpaceExplorer
            "vid_046d&pid_c625", // SpacePilot
            "vid_046d&pid_c623", // SpaceTraveler
            "vid_046d&pid_c621", // SpaceBall 5000
            "vid_046d&pid_c622", // SpaceBall 5000 USB
            "vid_03eb&pid_2013", // Aerion NDOF
        };

        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        private readonly Action<string> _log;
        private readonly object _sync = new object();
        private readonly short[] _axes = new short[6];
        private uint _buttons;
        private IntPtr _handle = InvalidHandle;
        private Thread _thread;
        private volatile bool _run;

        public HidReader(Action<string> log)
        {
            _log = log;
        }

        public void Start()
        {
            _run = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "openndof-sw-hid" };
            _thread.Start();
        }

        /// <summary>Latest raw axes (tx, ty, tz, rx, ry, rz) and button mask.</summary>
        public double[] Read(out uint buttons)
        {
            lock (_sync)
            {
                buttons = _buttons;
                return new double[] { _axes[0], _axes[1], _axes[2], _axes[3], _axes[4], _axes[5] };
            }
        }

        /// <summary>Stops the reader thread; returns within ~WaitSliceMs even if the device is idle.</summary>
        public void Dispose()
        {
            _run = false;
            _thread?.Join(2 * WaitSliceMs);
        }

        private void Loop()
        {
            IntPtr overlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OVERLAPPED)));
            IntPtr buffer = Marshal.AllocHGlobal(ReportBufferSize);
            IntPtr ioEvent = CreateEvent(IntPtr.Zero, true, false, null);
            var report = new byte[ReportBufferSize];
            bool verbose = true;
            try
            {
                while (_run)
                {
                    if (_handle == InvalidHandle)
                    {
                        _handle = Open(verbose);
                        verbose = false;
                        if (_handle == InvalidHandle)
                        {
                            SleepWhileRunning(2000);
                            continue;
                        }
                    }

                    int read = ReadReport(overlapped, ioEvent, buffer);
                    if (read > 0)
                    {
                        Marshal.Copy(buffer, report, 0, read);
                        Push(report, read);
                    }
                    else if (read < 0 && _run)
                    {
                        _log("HID: read interrupted (device unplugged?), retrying");
                        CloseDevice();
                        verbose = true;
                        SleepWhileRunning(500);
                    }
                }
            }
            finally
            {
                CloseDevice();
                CloseHandle(ioEvent);
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(overlapped);
            }
        }

        /// <summary>
        /// One overlapped read. Returns the byte count, 0 when stopping (I/O cancelled),
        /// or -1 on a device error.
        /// </summary>
        private int ReadReport(IntPtr overlapped, IntPtr ioEvent, IntPtr buffer)
        {
            ResetEvent(ioEvent);
            Marshal.StructureToPtr(new OVERLAPPED { EventHandle = ioEvent }, overlapped, false);
            if (!ReadFile(_handle, buffer, (uint)ReportBufferSize, IntPtr.Zero, overlapped)
                && Marshal.GetLastWin32Error() != ErrorIoPending)
            {
                return -1;
            }

            while (WaitForSingleObject(ioEvent, WaitSliceMs) == WaitTimeout)
            {
                if (!_run)
                {
                    CancelIoEx(_handle, overlapped);
                    GetOverlappedResult(_handle, overlapped, out _, true);  // buffers must outlive the I/O
                    return 0;
                }
            }

            return GetOverlappedResult(_handle, overlapped, out uint read, false) ? (int)read : -1;
        }

        private void SleepWhileRunning(int milliseconds)
        {
            for (int waited = 0; _run && waited < milliseconds; waited += WaitSliceMs)
            {
                Thread.Sleep(WaitSliceMs);
            }
        }

        private void CloseDevice()
        {
            lock (_sync)
            {
                Array.Clear(_axes, 0, _axes.Length);
                _buttons = 0;
            }

            if (_handle != InvalidHandle)
            {
                CloseHandle(_handle);
                _handle = InvalidHandle;
            }
        }

        private static short Negate(short v) => v == short.MinValue ? short.MaxValue : (short)-v;

        private void Push(byte[] b, int n)
        {
            short Get(int i) => (short)(b[i] | (b[i + 1] << 8));
            lock (_sync)
            {
                switch (b[0])
                {
                    case 0x01 when n >= 7:
                        _axes[0] = Get(1);
                        _axes[2] = Get(3);
                        _axes[1] = Negate(Get(5));
                        break;
                    case 0x02 when n >= 7:
                        _axes[3] = Get(1);
                        _axes[4] = Get(3);
                        _axes[5] = Get(5);
                        break;
                    case 0x03 when n >= 2:
                        uint mask = 0;
                        for (int i = 1; i < n && i < 5; i++)
                        {
                            mask |= (uint)b[i] << (8 * (i - 1));
                        }
                        _buttons = mask;
                        break;
                }
            }
        }

        private IntPtr Open(bool verbose)
        {
            string path = FindDevicePath();
            if (path == null)
            {
                if (verbose)
                {
                    _log("HID: no supported 3Dconnexion device found");
                }
                return InvalidHandle;
            }

            IntPtr h = CreateFile(path, GenericRead, FileShareReadWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
            if (h == InvalidHandle)
            {
                if (verbose)
                {
                    _log("HID: CreateFile failed, error " + Marshal.GetLastWin32Error());
                }
                return InvalidHandle;
            }

            _log("HID: " + path);
            return h;
        }

        /// <summary>First HID interface path matching <see cref="KnownDevices"/>, in list order.</summary>
        private static string FindDevicePath()
        {
            HidD_GetHidGuid(out Guid hidGuid);
            IntPtr set = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
            if (set == InvalidHandle)
            {
                return null;
            }

            try
            {
                string best = null;
                int bestRank = int.MaxValue;
                var data = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA)) };
                for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data); i++)
                {
                    string path = GetInterfacePath(set, ref data);
                    int rank = path == null ? -1 : Array.FindIndex(KnownDevices, id => path.Contains(id));
                    if (rank >= 0 && rank < bestRank)
                    {
                        best = path;
                        bestRank = rank;
                    }
                }
                return best;
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }
        }

        private static string GetInterfacePath(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data)
        {
            SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out int size, IntPtr.Zero);
            if (size <= 0)
            {
                return null;
            }

            IntPtr detail = Marshal.AllocHGlobal(size);
            try
            {
                // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize: 8 on x64 (DWORD + WCHAR[1], 8-aligned).
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, size, out _, IntPtr.Zero))
                {
                    return null;
                }
                return Marshal.PtrToStringUni(IntPtr.Add(detail, 4))?.ToLowerInvariant();
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }

        // â”€â”€ Win32 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private const uint DigcfPresent = 0x02, DigcfDeviceInterface = 0x10;
        private const uint GenericRead = 0x80000000, FileShareReadWrite = 3, OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000, WaitTimeout = 0x102;
        private const int ErrorIoPending = 997;
        private const int WaitSliceMs = 200;
        private const int ReportBufferSize = 64;

        [StructLayout(LayoutKind.Sequential)]
        private struct OVERLAPPED
        {
            public UIntPtr Internal;
            public UIntPtr InternalHigh;
            public uint Offset;
            public uint OffsetHigh;
            public IntPtr EventHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
            uint memberIndex, ref SP_DEVICE_INTERFACE_DATA data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data,
            IntPtr detail, int detailSize, out int requiredSize, IntPtr deviceInfoData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition,
            uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr h, IntPtr buffer, uint toRead, IntPtr read, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetOverlappedResult(IntPtr h, IntPtr overlapped, out uint transferred, bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(IntPtr h, IntPtr overlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateEvent(IntPtr security, bool manualReset, bool initialState, string name);

        [DllImport("kernel32.dll")]
        private static extern bool ResetEvent(IntPtr h);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr h, int milliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);
    }
}
