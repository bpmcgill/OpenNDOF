using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace OpenNDOF.Core.Devices;

/// <summary>
/// Wraps the Logitech LCD SDK (lglcd.dll) to render text on the SpacePilot
/// monochrome LCD (160×43 pixels, 1 byte per pixel).
///
/// Lifecycle: Init → Connect → Open → UpdateBitmap → Close → Disconnect → DeInit
/// </summary>
public sealed class LgLcdService : IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const int BmpWidth  = 160;
    private const int BmpHeight = 43;
    // lglcd.dll Bitmap.Pixels array is always SizeConst=307200 regardless of device
    private const int PixelsArraySize = 307200;

    private const uint PriorityNormal      = 128;
    private const uint SyncUpdate          = 0x80000000;
    private const uint UpdatePriority      = SyncUpdate | PriorityNormal;

    private const int  InvalidHandle       = -1;
    private const int  ErrorSuccess        = 0;

    // ── lglcd.dll P/Invoke ────────────────────────────────────────────────────

    private const string NativeDll = "GammaJul.LgLcd.Native64.dll";

    private enum BitmapFormat { Monochrome = 1 }
    private enum DeviceType   { Monochrome = 1 }
    private enum AppletCapabilities { Bw = 1 }

    // ConfigureContext and NotificationContext are each { delegate-pointer, IntPtr }
    // We pass null (zero) for both — represented as two IntPtrs each.
    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Auto)]
    private struct ConnectContextEx
    {
        [MarshalAs(UnmanagedType.LPTStr)]
        public string AppFriendlyName;
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsPersistent;
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsAutostartable;
        // ConfigureContext { ConfigureDelegate, IntPtr } — null
        public IntPtr OnConfigure;
        public IntPtr OnConfigureContext;
        public int    Connection;    // out
        public AppletCapabilities AppletCapabilitiesSupported;
        public int    Reserved1;
        // NotificationContext { NotificationDelegate, IntPtr } — null
        public IntPtr OnNotify;
        public IntPtr OnNotifyContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SoftbuttonsChangedContext
    {
        public IntPtr OnSoftbuttonsChanged;
        public IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenByTypeContext
    {
        public int                       Connection;
        public DeviceType                DeviceType;
        public SoftbuttonsChangedContext OnSoftbuttonsChanged;
        public int                       Device;  // out
    }

    // Class (not struct) because the marshaller can't handle 307200-byte structs on the stack
    [StructLayout(LayoutKind.Sequential)]
    private class LgBitmap
    {
        public BitmapFormat Format;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PixelsArraySize)]
        public byte[] Pixels = new byte[PixelsArraySize];
    }

    [DllImport(NativeDll, EntryPoint = "lgLcdInit")]
    private static extern int NativeLgLcdInit();

    [DllImport(NativeDll, EntryPoint = "lgLcdDeInit")]
    private static extern int NativeLgLcdDeInit();

    [DllImport(NativeDll, EntryPoint = "lgLcdConnectEx", CharSet = CharSet.Auto)]
    private static extern int NativeLgLcdConnectEx(ref ConnectContextEx ctx);

    [DllImport(NativeDll, EntryPoint = "lgLcdDisconnect")]
    private static extern int NativeLgLcdDisconnect(int connection);

    [DllImport(NativeDll, EntryPoint = "lgLcdOpenByType")]
    private static extern int NativeLgLcdOpenByType(ref OpenByTypeContext ctx);

    [DllImport(NativeDll, EntryPoint = "lgLcdClose")]
    private static extern int NativeLgLcdClose(int device);

    [DllImport(NativeDll, EntryPoint = "lgLcdUpdateBitmap")]
    private static extern int NativeLgLcdUpdateBitmap(int device, [In] LgBitmap bitmap, uint priority);

    [DllImport(NativeDll, EntryPoint = "lgLcdSetAsLCDForegroundApp")]
    private static extern int NativeLgLcdSetAsLCDForegroundApp(int device, [MarshalAs(UnmanagedType.Bool)] bool foreground);

    // ── State ─────────────────────────────────────────────────────────────────

    private int  _connection = InvalidHandle;
    private int  _device     = InvalidHandle;
    private bool _initialized;
    private bool _disposed;

    public bool IsReady => _device != InvalidHandle;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialise the SDK, connect as an applet, and open the mono LCD device.
    /// Returns <c>true</c> on success.
    /// </summary>
    public bool Open(string appName = "OpenNDOF")
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LgLcdService));

        System.Diagnostics.Debug.WriteLine($"[LgLcd] Calling lgLcdInit...");
        int rc = NativeLgLcdInit();
        System.Diagnostics.Debug.WriteLine($"[LgLcd] lgLcdInit rc={rc} (0=ok, 1167=service not running)");
        if (rc != ErrorSuccess) return false;
        _initialized = true;

        var ctx = new ConnectContextEx
        {
            AppFriendlyName              = appName,
            IsAutostartable              = false,
            IsPersistent                 = false,
            AppletCapabilitiesSupported  = AppletCapabilities.Bw,
            Connection                   = InvalidHandle,
        };

        rc = NativeLgLcdConnectEx(ref ctx);
        System.Diagnostics.Debug.WriteLine($"[LgLcd] lgLcdConnectEx rc={rc}");
        if (rc != ErrorSuccess) { NativeLgLcdDeInit(); _initialized = false; return false; }
        _connection = ctx.Connection;

        var openCtx = new OpenByTypeContext
        {
            Connection = _connection,
            DeviceType = DeviceType.Monochrome,
        };

        rc = NativeLgLcdOpenByType(ref openCtx);
        System.Diagnostics.Debug.WriteLine($"[LgLcd] lgLcdOpenByType rc={rc}");
        if (rc != ErrorSuccess) { Close(); return false; }
        _device = openCtx.Device;

        NativeLgLcdSetAsLCDForegroundApp(_device, true);
        return true;
    }

    public void Close()
    {
        if (_device != InvalidHandle)
        {
            NativeLgLcdSetAsLCDForegroundApp(_device, false);
            NativeLgLcdClose(_device);
            _device = InvalidHandle;
        }
        if (_connection != InvalidHandle)
        {
            NativeLgLcdDisconnect(_connection);
            _connection = InvalidHandle;
        }
        if (_initialized)
        {
            NativeLgLcdDeInit();
            _initialized = false;
        }
    }

    // ── Display write ─────────────────────────────────────────────────────────

    /// <summary>
    /// Renders two text lines and pushes them to the LCD.
    /// Returns <c>true</c> on success.
    /// </summary>
    public bool WriteText(string line0, string line1)
    {
        if (!IsReady) return false;

        var bmp = new LgBitmap { Format = BitmapFormat.Monochrome };
        RenderToPixels(line0, line1, bmp.Pixels);

        int rc = NativeLgLcdUpdateBitmap(_device, bmp, UpdatePriority);
        return rc == ErrorSuccess;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static void RenderToPixels(string line0, string line1, byte[] pixels)
    {
        // lglcd mono bitmap: 1 byte per pixel, 0=black 255=white, row-major 160×43
        using var bmp = new System.Drawing.Bitmap(BmpWidth, BmpHeight, PixelFormat.Format32bppArgb);
        using var g   = System.Drawing.Graphics.FromImage(bmp);

        g.Clear(System.Drawing.Color.Black);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        using var font  = new System.Drawing.Font("Consolas", 11f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);

        g.DrawString(line0, font, brush, 0f, 1f);
        g.DrawString(line1, font, brush, 0f, 23f);

        // Extract to flat pixel array (1 byte per pixel)
        var rect = new System.Drawing.Rectangle(0, 0, BmpWidth, BmpHeight);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int    stride = data.Stride;
            byte[] raw    = new byte[Math.Abs(stride) * BmpHeight];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);

            for (int y = 0; y < BmpHeight; y++)
                for (int x = 0; x < BmpWidth; x++)
                {
                    byte r = raw[y * stride + x * 4 + 2];  // BGRA — red channel
                    pixels[y * BmpWidth + x] = r;           // 0 or 255
                }
        }
        finally { bmp.UnlockBits(data); }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}
