using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using OpenNDOF.HID;

namespace OpenNDOF.Core.Devices;

/// <summary>
/// Renders text to the SpacePilot 240×64 monochrome LCD display using the
/// 3DConnexion HID feature-report protocol (reverse-engineered by jtsiomb/3dxdisp):
///
///   0x0C  LCD_POS  [reportId, page(0-7), col(0-239), 0x00]  – set write cursor
///   0x0D  LCD_DATA [reportId, c0, c1, c2, c3, c4, c5, 0x00] – 6 column bytes + 1 pad
///
/// Each page is a horizontal band of 8 rows. Each column byte encodes 8 vertical
/// pixels: bit 7 = topmost row of the page, bit 0 = bottommost.
///
/// The firmware continuously redraws the LCD from its own GRAM. To prevent it from
/// overwriting our writes, report 0x12 with byte[3]=0x2F must be sent as a suppressor
/// before each frame write.
/// </summary>
internal static class SpacePilotLcd
{
    public const int Width  = 240;
    public const int Height = 64;
    public const int Pages  = Height / 8;   // 8 pages

    // Font metrics for the default render font (Segoe UI 12px).
    // At this size each line is ~13px tall, giving comfortable 5-line capacity.
    // Emoji are supported — Segoe UI Emoji renders at the same size.
    private const string FontFamily   = "Segoe UI";
    private const float  FontSizePx   = 12f;
    private const int    LineHeightPx = 13;
    private const int    TopPaddingPx = 1;

    /// <summary>Maximum number of lines that fit on the 64px display.</summary>
    public const int MaxLines = Height / LineHeightPx;           // = 4 (with 1px top pad) or 5 tight

    /// <summary>Approximate max characters per line at the default font.</summary>
    public const int CharsPerLine = (int)(Width / (FontSizePx * 0.55f)); // ≈ 36

    private const byte ReportLcdPos      = 0x0C;
    private const byte ReportLcdData     = 0x0D;
    private const byte ReportSuppress    = 0x12;
    private const int  ColsPerDataReport = 7;   // bytes [1..7] are pixel data; device advances 7 columns per report

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears the LCD to all black. Should be called on device disconnect or
    /// application shutdown — GRAM persists as long as USB power is present,
    /// so the last frame will remain visible indefinitely without an explicit clear.
    /// </summary>
    public static bool Clear(IHidDevice device)
        => SendPages(device, new byte[Pages, Width]);

    /// <summary>
    /// Fills the entire LCD with white pixels as a hardware sanity test.
    /// </summary>
    public static bool FillWhite(IHidDevice device)
    {
        var pages = new byte[Pages, Width];
        for (int page = 0; page < Pages; page++)
            for (int col = 0; col < Width; col++)
                pages[page, col] = 0xFF;
        return SendPages(device, pages);
    }

    // Layout constants for the button-grid overlay
    private const int HeaderHeightPx = 16;   // app-name banner
    private const int GridRowHeightPx = 16;  // 3 rows × 16px = 48px (total = 64px)
    private const int ColWidthPx      = 120; // 2 columns × 120px = 240px

    /// <summary>
    /// Renders the SpacePilot-style overlay: app name banner at the top,
    /// then a 2-column × 3-row grid for the 6 macro button labels.
    /// Left column = buttons 1, 3, 5 (indices 0, 2, 4).
    /// Right column = buttons 2, 4, 6 (indices 1, 3, 5).
    /// </summary>
    public static bool WriteButtonGrid(IHidDevice device, string appName, string[] labels)
    {
        // Ensure we always have exactly 6 slots
        var l = new string[6];
        for (int i = 0; i < 6; i++)
            l[i] = (labels is not null && i < labels.Length) ? labels[i] : "";

        byte[] flatFb = RenderButtonGrid(appName, l);
        byte[,] pages = ToPageBuffer(flatFb);
        return SendPages(device, pages);
    }

    /// <summary>
    /// Renders up to <see cref="MaxLines"/> lines of text (including emoji) to the LCD.
    /// Lines beyond the display capacity are silently truncated.
    /// </summary>
    public static bool WriteText(IHidDevice device, params string[] lines)
    {
        byte[] flatFb = RenderLines(lines);
        byte[,] pages = ToPageBuffer(flatFb);
        return SendPages(device, pages);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static byte[] RenderButtonGrid(string appName, string[] l)
    {
        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(Color.Black);

        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix
                  | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        // ── Header: app name (bold, full width) ──────────────────────────────
        using var boldFont = new Font(FontFamily, FontSizePx, FontStyle.Bold, GraphicsUnit.Pixel);
        var headerRect = new Rectangle(0, 0, Width, HeaderHeightPx);
        // Fill header background to distinguish it from the grid
        g.FillRectangle(Brushes.White, headerRect);
        TextRenderer.DrawText(g, appName, boldFont, headerRect,
            Color.Black, Color.White,
            flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // ── Divider line ─────────────────────────────────────────────────────
        g.DrawLine(Pens.White, 0, HeaderHeightPx, Width, HeaderHeightPx);

        // ── Button grid ──────────────────────────────────────────────────────
        using var font = new Font(FontFamily, FontSizePx, FontStyle.Regular, GraphicsUnit.Pixel);

        // row r → left button index = r*2, right = r*2+1
        for (int row = 0; row < 3; row++)
        {
            int y = HeaderHeightPx + 1 + row * GridRowHeightPx;

            for (int col = 0; col < 2; col++)
            {
                int idx   = row * 2 + col;
                string lbl = l[idx];
                string text = string.IsNullOrWhiteSpace(lbl)
                    ? $"{idx + 1}:"
                    : $"{idx + 1}: {lbl}";

                int x = col * ColWidthPx + 2;
                var rect = new Rectangle(x, y, ColWidthPx - 4, GridRowHeightPx - 1);
                TextRenderer.DrawText(g, text, font, rect,
                    Color.White, Color.Black, flags | TextFormatFlags.VerticalCenter);
            }

            // Vertical divider between columns
            int divX = ColWidthPx;
            g.DrawLine(Pens.White, divX, HeaderHeightPx + 1, divX, Height - 1);
        }

        return BitmapToFlatBuffer(bmp);
    }

    /// <summary>
    /// Renders lines of text onto a 240×64 bitmap.
    /// Uses ClearType anti-aliasing then thresholds at 40% brightness for clean
    /// 1-bit output — far superior to SingleBitPerPixelGridFit on a mono LCD.
    /// Supports emoji via Segoe UI Emoji fallback (GDI+ uses font fallback automatically).
    /// </summary>
    private static byte[] RenderLines(string[] lines)
    {
        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);

        g.Clear(Color.Black);

        using var font = new Font(FontFamily, FontSizePx, FontStyle.Regular, GraphicsUnit.Pixel);

        // TextRenderer (GDI) is used instead of GDI+ DrawString because GDI performs
        // proper font fallback to Segoe UI Emoji, which GDI+ does not support.
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix
                  | TextFormatFlags.SingleLine | TextFormatFlags.NoClipping;

        int maxLines = Math.Min(lines.Length, MaxLines);
        for (int i = 0; i < maxLines; i++)
        {
            int y = TopPaddingPx + i * LineHeightPx;
            if (y + LineHeightPx > Height) break;
            TextRenderer.DrawText(g, lines[i], font, new Point(0, y), Color.White, Color.Black, flags);
        }

        return BitmapToFlatBuffer(bmp);
    }

    /// <summary>
    /// Extracts pixel brightness into a flat byte array [y * Width + x].
    /// Uses LockBits for performance instead of GetPixel.
    /// </summary>
    private static byte[] BitmapToFlatBuffer(Bitmap bmp)
    {
        var flat = new byte[Width * Height];
        var rect = new Rectangle(0, 0, Width, Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride  = data.Stride;
            var raw     = new byte[Math.Abs(stride) * Height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                    {
                        // ARGB layout in memory: B, G, R, A — use red channel for brightness
                        int base_ = y * stride + x * 4;
                        // Threshold at 40% brightness — keeps clean glyph cores, drops anti-alias fringe
                        byte val = raw[base_ + 2] >= 102 ? (byte)255 : (byte)0;
                        flat[y * Width + x] = val;
                    }
            }
        }
        finally { bmp.UnlockBits(data); }

        return flat;
    }

    // ── Page conversion ───────────────────────────────────────────────────────

    /// <summary>
    /// Converts a flat brightness buffer to the page-ordered column format
    /// required by the device: [page, col] where each byte encodes 8 vertical
    /// pixels (bit 7 = top of the page, bit 0 = bottom — confirmed by hardware probe).
    /// </summary>
    private static byte[,] ToPageBuffer(byte[] flat)
    {
        var buf = new byte[Pages, Width];
        for (int page = 0; page < Pages; page++)
        {
            int rowBase = page * 8;
            for (int col = 0; col < Width; col++)
            {
                byte b = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int row = rowBase + bit;
                    // bit0 = top row of page, bit7 = bottom row (matches jtsiomb/3dxdisp reference)
                    if (row < Height && flat[row * Width + col] > 0)
                        b |= (byte)(1 << bit);
                }
                buf[page, col] = b;
            }
        }
        return buf;
    }

    // ── HID transmission ──────────────────────────────────────────────────────

    private static bool SendPages(IHidDevice device, byte[,] pages)
    {
        // Suppress firmware LCD redraws before we write our frame.
        // Without this the firmware overwrites our GRAM writes within ~1-2s.
        var suppressBuf = new byte[8];
        suppressBuf[0] = ReportSuppress;
        suppressBuf[3] = 0x2F;
        device.WriteFeatureReport(suppressBuf);

        bool allOk = true;
        for (int page = 0; page < Pages; page++)
        {
            bool posOk = SetPosition(device, page, 0);
            if (!posOk)
            {
                System.Diagnostics.Debug.WriteLine($"[LCD] SetPosition failed on page {page}");
                allOk = false;
                continue;
            }

            int col = 0;
            while (col < Width)
            {
                int count = Math.Min(ColsPerDataReport, Width - col);

                var buf = new byte[8];
                buf[0] = ReportLcdData;
                for (int i = 0; i < count; i++)
                    buf[1 + i] = pages[page, col + i];

                if (!device.WriteFeatureReport(buf))
                {
                    System.Diagnostics.Debug.WriteLine($"[LCD] WriteFeatureReport failed page={page} col={col}");
                    allOk = false;
                }

                col += count;
            }
        }
        return allOk;
    }

    private static bool SetPosition(IHidDevice device, int page, int col)
    {
        // HidD_SetFeature requires the buffer to equal FeatureReportByteLength (8).
        // The device only reads the first 4 bytes; the rest must be zero-padded.
        var buf = new byte[8];
        buf[0] = ReportLcdPos;
        buf[1] = (byte)page;
        buf[2] = (byte)col;
        buf[3] = 0;
        return device.WriteFeatureReport(buf);
    }
}
