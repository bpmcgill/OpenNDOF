using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

HidProbe.Run();

static class HidProbe
{
    const ushort VID = 0x046D, PID = 0xC625;

    public static void Run()
    {
        // First: enumerate ALL HID interfaces for C625 and print their caps
        var allPaths = FindAllDevices(VID, PID);
        Console.WriteLine($"Found {allPaths.Count} HID interface(s) for C625:");
        foreach (var p in allPaths)
        {
            var hh = CreateFile(p, 0x80000000, 1 | 2, 0, 3, 0, 0); // read-only share
            if (hh == -1) { Console.WriteLine("  [OPEN FAILED " + Marshal.GetLastWin32Error() + "] " + p); continue; }
            HidD_GetPreparsedData(hh, out var pp2);
            HidP_GetCaps(pp2, out var caps2);
            HidD_FreePreparsedData(pp2);
            CloseHandle(hh);
            Console.WriteLine($"  In={caps2.InputReportLen} Out={caps2.OutputReportLen} Feat={caps2.FeatureReportLen} FeatVCaps={caps2.NumberFeatureValueCaps}  {p}");
        }

        // Also try USB device interface (GUID_DEVINTERFACE_USB_DEVICE)
        Console.WriteLine("\nLooking for USB device interface...");
        var usbPaths = FindAllDevices(VID, PID, new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED"));
        foreach (var p in usbPaths) Console.WriteLine("  USB: " + p);

        // Try WinUSB on USB device path
        foreach (var usbPath in usbPaths)
        {
            Console.WriteLine("\nTrying WinUSB on: " + usbPath);
            var hu = CreateFile(usbPath, 0x40000000, 1 | 2, 0, 3, 0x00000080, 0);
            if (hu == -1) { Console.WriteLine("  CreateFile failed: " + Marshal.GetLastWin32Error()); continue; }
            Console.WriteLine("  CreateFile ok");
            bool wiOk = WinUsb_Initialize(hu, out var wi);
            if (!wiOk) { Console.WriteLine("  WinUsb_Initialize failed: " + Marshal.GetLastWin32Error()); CloseHandle(hu); continue; }
            Console.WriteLine("  WinUsb_Initialize ok!");

            // Query interface settings — find alternate interface with bulk OUT pipe
            for (byte altIdx = 0; altIdx < 4; altIdx++)
            {
                bool qOk = WinUsb_QueryInterfaceSettings(wi, altIdx, out var ifDesc);
                if (!qOk) break;
                Console.WriteLine($"  AltSetting[{altIdx}]: class={ifDesc.bInterfaceClass:X2} sub={ifDesc.bInterfaceSubClass:X2} proto={ifDesc.bInterfaceProtocol:X2} numEP={ifDesc.bNumEndpoints}");
                for (byte ep = 0; ep < ifDesc.bNumEndpoints; ep++)
                {
                    WinUsb_QueryPipe(wi, altIdx, ep, out var pipeInfo);
                    Console.WriteLine($"    Pipe[{ep}]: addr=0x{pipeInfo.PipeId:X2} type={pipeInfo.PipeType} maxPkt={pipeInfo.MaximumPacketSize}");
                }
            }

            // Try writing test data to the OUT pipe (addr 0x01 or 0x02 typically)
            Console.WriteLine("  Trying WritePipe on ep 0x01 and 0x02...");
            foreach (byte epAddr in new byte[] { 0x01, 0x02, 0x03 })
            {
                var testBuf = new byte[64];
                testBuf[0] = 0x05; // report-style header guess
                bool wOk = WinUsb_WritePipe(wi, epAddr, testBuf, (uint)testBuf.Length, out uint written, nint.Zero);
                Console.WriteLine($"  WritePipe ep=0x{epAddr:X2} ok={wOk} written={written} err={Marshal.GetLastWin32Error()}");
            }

            WinUsb_Free(wi);
            CloseHandle(hu);
        }
        Console.WriteLine();

        // Pick the interface with the largest FeatureReportLen — that's the LCD/command interface
        string? path = null;
        int bestFeat = 0;
        foreach (var p in allPaths)
        {
            var hh = CreateFile(p, 0x80000000, 1 | 2, 0, 3, 0, 0);
            if (hh == -1) continue;
            HidD_GetPreparsedData(hh, out var pp2);
            HidP_GetCaps(pp2, out var caps2);
            HidD_FreePreparsedData(pp2);
            CloseHandle(hh);
            if (caps2.FeatureReportLen > bestFeat) { bestFeat = caps2.FeatureReportLen; path = p; }
        }
        if (path is null) { Console.WriteLine("SpacePilot not found - is it plugged in?"); return; }
        Console.WriteLine($"Selected interface: FeatureReportLen={bestFeat}  {path}");

        var h = CreateFile(path, 0x80000000 | 0x40000000, 1 | 2, 0, 3, 0, 0);
        if (h == -1) { Console.WriteLine("Open failed: " + Marshal.GetLastWin32Error()); return; }
        Console.WriteLine("Opened OK");

        HidD_GetPreparsedData(h, out var pp);
        HidP_GetCaps(pp, out var caps);
        Console.WriteLine("Input=" + caps.InputReportLen + " Output=" + caps.OutputReportLen + " Feature=" + caps.FeatureReportLen);
        Console.WriteLine("Feature ValueCaps=" + caps.NumberFeatureValueCaps + " ButtonCaps=" + caps.NumberFeatureButtonCaps);

        if (caps.NumberFeatureValueCaps > 0)
        {
            var vc = new HIDP_VALUE_CAPS[caps.NumberFeatureValueCaps];
            ushort vcLen = (ushort)vc.Length;
            HidP_GetValueCaps(2, vc, ref vcLen, pp);
            foreach (var v in vc)
                Console.WriteLine("  FeatureValue: ReportID=0x" + v.ReportID.ToString("X2") + " UsagePage=0x" + v.UsagePage.ToString("X4") + " Usage=0x" + v.UsageOrMin.ToString("X4") + " BitSize=" + v.BitSize + " Count=" + v.ReportCount + " LogMin=" + v.LogMin + " LogMax=" + v.LogMax);
        }
        if (caps.NumberFeatureButtonCaps > 0)
        {
            var bc = new HIDP_BUTTON_CAPS[caps.NumberFeatureButtonCaps];
            ushort bcLen = (ushort)bc.Length;
            HidP_GetButtonCaps(2, bc, ref bcLen, pp);
            foreach (var b in bc)
                Console.WriteLine("  FeatureButton: ReportID=0x" + b.ReportID.ToString("X2") + " UsagePage=0x" + b.UsagePage.ToString("X4"));
        }
        HidD_FreePreparsedData(pp);

        // ---- LCD interactive probe ----
        var logPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "hidprobe_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
        using var log = new System.IO.StreamWriter(logPath, append: false) { AutoFlush = true };
        Console.WriteLine("\n--- LCD interactive probe ---");
        Console.WriteLine("LOG FILE: " + logPath);

        // Quick SetFeature sanity check using correct FeatureReportLen
        // We don't know featLen yet (caps read happens after this), so read it first
        HidD_GetPreparsedData(h, out var ppSanity);
        HidP_GetCaps(ppSanity, out var capsSanity);
        HidD_FreePreparsedData(ppSanity);
        var testSuppress = new byte[capsSanity.FeatureReportLen]; testSuppress[0] = 0x12; testSuppress[3] = 0x2F;
        bool sfOk = HidD_SetFeature(h, testSuppress, testSuppress.Length);
        int sfErr = Marshal.GetLastWin32Error();
        Console.WriteLine($"SetFeature sanity check: ok={sfOk} err={sfErr} bufLen={testSuppress.Length} (0=success, 5=access denied)");
        log.WriteLine($"SetFeature sanity: ok={sfOk} err={sfErr} bufLen={testSuppress.Length}");
        if (!sfOk)
        {
            Console.WriteLine("*** SetFeature FAILED — wrong interface or access denied. Trying all interfaces...");
            foreach (var altPath in allPaths)
            {
                var altH = CreateFile(altPath, 0x80000000 | 0x40000000, 1 | 2, 0, 3, 0, 0);
                if (altH == -1) { Console.WriteLine($"  OPEN FAILED ({Marshal.GetLastWin32Error()}): {altPath}"); continue; }
                bool ok2 = HidD_SetFeature(altH, testSuppress, testSuppress.Length);
                int err2 = Marshal.GetLastWin32Error();
                Console.WriteLine($"  SetFeature ok={ok2} err={err2}: {altPath}");
                if (ok2) { CloseHandle(h); h = altH; Console.WriteLine("  *** Switched to this interface!"); break; }
                CloseHandle(altH);
            }
        }

        Console.WriteLine("For each test: look at device, then press ENTER and type answer.\n");
        log.WriteLine("HidProbe session " + DateTime.Now.ToString("u"));
        log.WriteLine("===========================================");

        string Ask(string question)
        {
            Console.Write("  >> " + question + " : ");
            var ans = Console.ReadLine() ?? "";
            if (ans == "") ans = "(enter)";
            log.WriteLine("  Q: " + question);
            log.WriteLine("  A: " + ans);
            return ans.Trim().ToLowerInvariant();
        }

        // CONFIRMED PROTOCOL (from probe logs):
        //   GRAM confirmed — single writes persist until firmware overwrites
        //   Pages 0-7 confirmed: each = one 8px-tall band, stepping down = 64px total
        //   Each 0x0D write = 6px wide × 8px tall block (6 payload bytes, 1 per pixel column)
        //   0x0C = [0x0C, page(0-7), col, 0x00]
        //   col=0,14,28,42 each place a block ~6-8px to the right of the previous
        //   ? col is pixel-addressed (1 col-unit ? 1 pixel), gap = 14-6 = 8px between blocks
        //   0x12 suppress[3]=0x2F = best firmware suppressor

        // ALL buffers MUST be exactly FeatureReportLen bytes — 3dxhid.sys returns ok=True even
        // for undersized buffers but the device silently ignores truncated packets.
        int featLen = caps.FeatureReportLen; // = 8: 1 ID byte + 7 payload bytes
        Console.WriteLine($"Using FeatureReportLen={featLen} for all buffers.");
        log.WriteLine($"FeatureReportLen={featLen}");

        // suppress: report 0x12, byte[3]=0x2F (indices are into the full featLen buffer)
        var suppress = new byte[featLen]; suppress[0] = 0x12; suppress[3] = 0x2F;
        void Suppress() => HidD_SetFeature(h, suppress, suppress.Length);

        // addrBuf: report 0x0C, [1]=page, [2]=col, [3]=0x00, rest=0
        var addrBuf = new byte[featLen]; addrBuf[0] = 0x0C;
        // dataBuf: report 0x0D, [1..6]=6 pixel payload bytes, [7]=0x00 (padding)
        var dataBuf = new byte[featLen]; dataBuf[0] = 0x0D;

        void WriteBlock(int page, int col, byte b0, byte b1, byte b2, byte b3, byte b4, byte b5)
        {
            addrBuf[1] = (byte)page; addrBuf[2] = (byte)col; addrBuf[3] = 0x00;
            dataBuf[1] = b0; dataBuf[2] = b1; dataBuf[3] = b2;
            dataBuf[4] = b3; dataBuf[5] = b4; dataBuf[6] = b5; dataBuf[7] = 0x00;
            HidD_SetFeature(h, addrBuf, addrBuf.Length);
            HidD_SetFeature(h, dataBuf, dataBuf.Length);
        }

        // Clear a single page (fast: only one page × cols 0..220)
        void ClearPage(int pg)
        {
            Suppress();
            for (int c = 0; c <= 220; c++)
                WriteBlock(pg, c, 0, 0, 0, 0, 0, 0);
        }

        // Clear all 8 pages (use sparingly — ~1800 HID calls)
        void ClearAll()
        {
            for (int s = 0; s < 3; s++) Suppress();
            for (int pg = 0; pg < 8; pg++)
                for (int c = 0; c <= 220; c++)
                    WriteBlock(pg, c, 0, 0, 0, 0, 0, 0);
        }

        // ---- Step 1: full clear — establish clean baseline ----
        log.WriteLine("\n[Step 1] Full clear baseline");
        Console.WriteLine("[Step 1] Clearing all pages (one-time baseline)...");
        ClearAll();
        System.Threading.Thread.Sleep(1500);
        Ask("Step1: display completely blank? (y/n/partial/note)");

        // ---- Step 2a: col adjacency — col=0 and col=6 (should touch if pixel-addressed) ----
        // We expect col=14 places a block at pixel 14, meaning pixels 6-13 are empty (8px gap).
        // So col=6 should produce a block immediately touching col=0 (pixels 0-5 + 6-11 = 12px wide bar).
        log.WriteLine("\n[Step 2a] Col adjacency: col=0 + col=6");
        Console.WriteLine("[Step 2a] col=0 + col=6 — should touch if 1 col-unit = 1 pixel...");
        ClearPage(3); Suppress();
        WriteBlock(3, 0,  0xFF,0xFF,0xFF,0xFF,0xFF,0xFF);
        WriteBlock(3, 6,  0xFF,0xFF,0xFF,0xFF,0xFF,0xFF);
        System.Threading.Thread.Sleep(2000);
        Ask("col=0 + col=6: one continuous 12px bar (touching), or gap between them? (touching/gap/note)");

        // ---- Step 2b: col=0 and col=7 (1px gap if pixel-addressed) ----
        log.WriteLine("\n[Step 2b] Col adjacency: col=0 + col=7");
        Console.WriteLine("[Step 2b] col=0 + col=7 — should have 1px gap if pixel-addressed...");
        ClearPage(3); Suppress();
        WriteBlock(3, 0,  0xFF,0xFF,0xFF,0xFF,0xFF,0xFF);
        WriteBlock(3, 7,  0xFF,0xFF,0xFF,0xFF,0xFF,0xFF);
        System.Threading.Thread.Sleep(2000);
        Ask("col=0 + col=7: touching or tiny 1px gap? (touching/tinygap/cleargap/note)");

        // ---- Step 2c: col=0 and col=1 (overlap test — same block if block-addressed) ----
        log.WriteLine("\n[Step 2c] Col overlap: col=0 + col=1 (different patterns)");
        Console.WriteLine("[Step 2c] col=0 as 0xAA, col=1 as 0x55 — overlapping or side-by-side?");
        ClearPage(3); Suppress();
        WriteBlock(3, 0,  0xAA,0xAA,0xAA,0xAA,0xAA,0xAA); // checkerboard
        WriteBlock(3, 1,  0x55,0x55,0x55,0x55,0x55,0x55); // inverse
        System.Threading.Thread.Sleep(2000);
        Ask("col=0 (0xAA) + col=1 (0x55): mixed/solid pattern (overlap), or two distinct side-by-side patterns? (mixed/sidebyside/note)");

        // ---- Step 3: right edge scan — write blocks at increasing col, find where they fall off ----
        // Expected: display ~160px wide ? last visible block around col=154
        log.WriteLine("\n[Step 3] Right edge scan");
        Console.WriteLine("[Step 3] Writing blocks at col 0,10,20,...200 — find right edge.");
        ClearPage(4); Suppress();
        for (int col = 0; col <= 200; col += 10)
            WriteBlock(4, col, 0xFF,0xFF,0xFF,0xFF,0xFF,0xFF);
        System.Threading.Thread.Sleep(3000);
        Ask("Which is the LAST visible block? What col value (approx)? (e.g. 'last at col=150, 160+ gone')");

        // ---- Step 4: byte order — which payload byte maps to the leftmost pixel column ----
        // byte[0] should be leftmost (pixel col = addrCol + 0)
        // Confirm by lighting only one byte at a time and seeing which side it appears on.
        log.WriteLine("\n[Step 4] Byte order within 0x0D payload");
        Console.WriteLine("[Step 4] byte[0]=0xFF only, then byte[5]=0xFF only — which is left?");
        ClearPage(3); Suppress();
        WriteBlock(3, 20, 0xFF,0,0,0,0,0); // only byte[0]
        System.Threading.Thread.Sleep(2000);
        Ask("byte[0]=0xFF (bytes 1-5 = 0): pixel appears LEFT or RIGHT side of the 6px block at col=20? (left/right/note)");
        ClearPage(3); Suppress();
        WriteBlock(3, 20, 0,0,0,0,0,0xFF); // only byte[5]
        System.Threading.Thread.Sleep(2000);
        Ask("byte[5]=0xFF (bytes 0-4 = 0): pixel appears LEFT or RIGHT side of the 6px block at col=20? (left/right/note)");

        // ---- Step 5: bit order — which bit maps to the top row of the 8px page band ----
        // On ST7565: bit0 = top row of page, bit7 = bottom row.
        // On some controllers: bit7 = top, bit0 = bottom.
        // Test on page 3 (middle-ish of screen). page 3 top edge is pixel row 24.
        log.WriteLine("\n[Step 5] Bit order within each payload byte");
        Console.WriteLine("[Step 5] bit7 (0x80) vs bit0 (0x01) — which row is TOP of page 3?");
        ClearPage(3); Suppress();
        WriteBlock(3, 20, 0x80,0x80,0x80,0x80,0x80,0x80); // bit7 only
        System.Threading.Thread.Sleep(2000);
        Ask("byte=0x80 (bit7): 1px line at TOP or BOTTOM of page 3's 8px band? (top/bottom/note)");
        ClearPage(3); Suppress();
        WriteBlock(3, 20, 0x01,0x01,0x01,0x01,0x01,0x01); // bit0 only
        System.Threading.Thread.Sleep(2000);
        Ask("byte=0x01 (bit0): 1px line at TOP or BOTTOM of page 3's 8px band? (top/bottom/note)");

        // ---- Step 6: full frame — write all pixels, all pages, full width ----
        // Final confirmation. Write every pixel white across the full confirmed width.
        log.WriteLine("\n[Step 6] Full frame solid white");
        Console.WriteLine("[Step 6] Full solid frame across all 8 pages...");
        for (int s = 0; s < 5; s++) Suppress();
        for (int pg = 0; pg < 8; pg++)
            for (int c = 0; c <= 200; c++)
                WriteBlock(pg, c, 0xFF,0xFF,0xFF,0xFF,0xFF,0xFF);
        System.Threading.Thread.Sleep(3000);
        Ask("Step6: how much of display is solid white? (full/most/partial/note)");

        log.WriteLine("\n===========================================");
        log.WriteLine("Session complete " + DateTime.Now.ToString("u"));
        Console.WriteLine("\nDone! Log: " + logPath);
        Console.WriteLine("Press ENTER to close...");
        Console.ReadLine();
        CloseHandle(h);
    }

    // Send a feature report with exact payload size (id byte + payload bytes)
    static void SetExact(nint h, byte id, params byte[] payload)
    {
        var r = new byte[1 + payload.Length];
        r[0] = id;
        payload.CopyTo(r, 1);
        HidD_SetFeature(h, r, r.Length);
    }

    static byte[] Enc(string s)
    {
        // Pad/trim to exactly 6 bytes
        var b = new byte[6];
        for (int i = 0; i < 6; i++) b[i] = i < s.Length && s[i] < 128 ? (byte)s[i] : (byte)' ';
        return b;
    }

    static string? FindDevice(ushort vid, ushort pid) => FindAllDevices(vid, pid).FirstOrDefault();

    static List<string> FindAllDevices(ushort vid, ushort pid, Guid? guidOverride = null)
    {
        var results = new List<string>();
        var g = guidOverride ?? new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        var devInfo = SetupDiGetClassDevs(ref g, null, 0, 0x12);
        if (devInfo == -1) return results;
        var ifData = new SPDD { cbSize = Marshal.SizeOf<SPDD>() };
        for (uint i = 0; SetupDiEnumDeviceInterfaces(devInfo, 0, ref g, i, ref ifData); i++)
        {
            int sz = 0;
            SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, 0, 0, ref sz, 0);
            if (sz == 0) continue;
            var buf = Marshal.AllocHGlobal(sz);
            Marshal.WriteInt32(buf, 8);
            SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, buf, sz, ref sz, 0);
            var p = Marshal.PtrToStringAuto(buf + 4);
            Marshal.FreeHGlobal(buf);
            if (p is null) continue;
            string lo = p.ToLowerInvariant();
            if (lo.Contains("vid_" + vid.ToString("x4")) && lo.Contains("pid_" + pid.ToString("x4")))
                results.Add(p);
        }
        SetupDiDestroyDeviceInfoList(devInfo);
        return results;
    }

    [DllImport("setupapi.dll", SetLastError=true, CharSet=CharSet.Auto)] static extern nint SetupDiGetClassDevs(ref Guid g, string? e, nint h, uint f);
    [DllImport("setupapi.dll", SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(nint di, nint dev, ref Guid g, uint i, ref SPDD d);
    [DllImport("setupapi.dll", SetLastError=true, CharSet=CharSet.Auto)] static extern bool SetupDiGetDeviceInterfaceDetail(nint di, ref SPDD id, nint dd, int s, ref int r, nint did);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(nint h);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)] static extern nint CreateFile(string n, uint a, uint s, nint sec, uint cd, uint f, nint t);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint h);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteFile(nint h, byte[] buf, uint len, out uint written, nint ov);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(nint h, out nint pp);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(nint pp);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(nint pp, out HIDP_CAPS c);
    [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int rt, [Out] HIDP_VALUE_CAPS[] vc, ref ushort len, nint pp);
    [DllImport("hid.dll")] static extern int HidP_GetButtonCaps(int rt, [Out] HIDP_BUTTON_CAPS[] bc, ref ushort len, nint pp);
    [DllImport("hid.dll")] static extern bool HidD_GetFeature(nint h, byte[] r, int len);
    [DllImport("hid.dll")] static extern bool HidD_SetFeature(nint h, byte[] r, int len);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_Initialize(nint deviceHandle, out nint interfaceHandle);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_Free(nint interfaceHandle);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_QueryInterfaceSettings(nint ih, byte altSettingNumber, out USB_INTERFACE_DESCRIPTOR usbAltInterfaceDescriptor);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_QueryPipe(nint ih, byte altSettingNumber, byte pipeIndex, out WINUSB_PIPE_INFORMATION pipeInformation);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_WritePipe(nint ih, byte pipeID, byte[] buffer, uint bufferLength, out uint lengthTransferred, nint overlapped);

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct USB_INTERFACE_DESCRIPTOR
    {
        public byte bLength, bDescriptorType, bInterfaceNumber, bAlternateSetting,
                    bNumEndpoints, bInterfaceClass, bInterfaceSubClass, bInterfaceProtocol, iInterface;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct WINUSB_PIPE_INFORMATION { public int PipeType; public byte PipeId; public ushort MaximumPacketSize; public byte Interval; }

    [StructLayout(LayoutKind.Sequential)] struct SPDD { public int cbSize; public Guid g; public uint f; public nint r; }
    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportLen, OutputReportLen, FeatureReportLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                      NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                      NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }
    // Real HIDP_VALUE_CAPS layout: 72 bytes total (Pack=1)
    // UsagePage(2) ReportID(1) IsAlias(1) BitField(2) LinkCollection(2) LinkUsage(2) LinkUsagePage(2)
    // IsRange(1) IsStringRange(1) IsDesignatorRange(1) IsAbsolute(1) HasNull(1) Reserved(1)
    // BitSize(2) ReportCount(2) Reserved2[5](10) UnitsExp(4) Units(4)
    // LogMin(4) LogMax(4) PhysMin(4) PhysMax(4) Union[8 ushorts](16)
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct HIDP_VALUE_CAPS
    {
        public ushort UsagePage;
        public byte ReportID, IsAlias;
        public ushort BitField, LinkCollection, LinkUsage, LinkUsagePage;
        public byte IsRange, IsStringRange, IsDesignatorRange, IsAbsolute, HasNull, Reserved;
        public ushort BitSize, ReportCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=5)] public ushort[] Reserved2;
        public uint UnitsExp, Units;
        public int LogMin, LogMax, PhysMin, PhysMax;
        // Union (Range or NotRange): 8 ushorts
        public ushort UsageOrMin, UsageOrMax, StringMin, StringMax, DesignatorMin, DesignatorMax, DataIndexMin, DataIndexMax;
    }
    // Real HIDP_BUTTON_CAPS layout: 72 bytes total (same size as VALUE_CAPS)
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct HIDP_BUTTON_CAPS
    {
        public ushort UsagePage;
        public byte ReportID, IsAlias;
        public ushort BitField, LinkCollection, LinkUsage, LinkUsagePage;
        public byte IsRange, IsStringRange, IsDesignatorRange, IsAbsolute;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=28)] public ushort[] Padding;
    }
}
