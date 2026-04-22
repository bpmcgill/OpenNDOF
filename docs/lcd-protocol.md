# SpacePilot LCD Protocol

Reference documentation for the 240×64 monochrome display fitted to the 3DConnexion SpacePilot.  
Protocol reverse-engineered by [jtsiomb/3dxdisp](https://github.com/jtsiomb/3dxdisp); this document describes OpenNDOF's implementation.

---

## Physical Display

| Property | Value |
|---|---|
| Resolution | 240 × 64 pixels |
| Colour depth | 1-bit monochrome |
| Interface | HID feature reports |
| Report size | 8 bytes |

---

## Page / Column Addressing

The display is divided into **8 horizontal pages**, each 8 pixels tall:

```
Page 0 → rows  0– 7   (top)
Page 1 → rows  8–15
...
Page 7 → rows 56–63   (bottom)
```

Within each page, columns run left-to-right from 0 to 239.

Each **column byte** encodes 8 vertical pixels:

```
bit 0 = top row of the page   (row 0 within the page)
bit 7 = bottom row of the page (row 7 within the page)
```

---

## HID Feature Reports

All communication uses `HidD_SetFeature` (8-byte buffers). Byte `[0]` is always the report ID.

### `0x12` — Suppress Firmware Redraws

Must be sent **before** each frame write. Without it, the SpacePilot firmware continuously refreshes the display from its own internal state and overwrites your writes within ~1–2 seconds.

```
Byte: [0x12, 0x00, 0x00, 0x2F, 0x00, 0x00, 0x00, 0x00]
```

### `0x0C` — Set Write Cursor (LCD_POS)

Positions the internal write cursor to the start of a page/column.

```
Byte: [0x0C, page, col, 0x00, 0x00, 0x00, 0x00, 0x00]
         ^     ^     ^
         |     |     column (0–239)
         |     page  (0–7)
         report ID
```

### `0x0D` — Write Column Data (LCD_DATA)

Writes **7 column bytes** to the display, advancing the cursor automatically. Remaining bytes must be zero-padded.

```
Byte: [0x0D, c0, c1, c2, c3, c4, c5, c6]
              ^^^^^^^^^^^^^^^^^^^^^^^^^^
              7 column bytes, each encoding 8 vertical pixels
```

> **Important:** The firmware advances the column pointer by exactly 7 after each `LCD_DATA` report. Always send exactly 7 bytes of pixel data per report, zero-padding at the end of a row if needed.

---

## Full Frame Write Sequence

```
1. Send 0x12 suppressor
2. For each page (0–7):
   a. Send 0x0C  [page, col=0]
   b. Send 0x0D  [c0..c6]       ← columns 0–6
   c. Send 0x0D  [c7..c13]      ← columns 7–13
   ...
   (34 × 0x0D reports to cover 240 columns; last report zero-pads the final 2 bytes)
```

Total reports per frame: `1 (suppress) + 8 × (1 pos + 35 data)` = **289 feature reports**.

---

## Text Rendering Pipeline

OpenNDOF renders text in software and sends the resulting bitmap to the display:

```
string[] lines
    │
    ▼
Graphics.DrawText (GDI TextRenderer)
    │   Font: "Segoe UI" 12px, GDI engine for emoji fallback
    │   Background: black, foreground: white
    ▼
32bpp ARGB Bitmap (240×64)
    │
    ▼
BitmapToFlatBuffer
    │   LockBits → read red channel → threshold at 40% (≥ 102 → lit)
    │   Result: byte[240 × 64], 0 = off, 255 = on
    ▼
ToPageBuffer
    │   Repack into byte[8, 240]  (page-major order)
    │   bit 0 = top of page, bit 7 = bottom
    ▼
SendPages  (HID feature reports as above)
```

### Font metrics at 12px Segoe UI

| Property | Value |
|---|---|
| Line height | 13 px |
| Top padding | 1 px |
| Max lines | 4 (64 px ÷ 13 px) |
| Approx. chars/line | ~36 |

---

## Clearing the Display

The display's GRAM is battery-backed: the last frame persists as long as USB power is present. Always send a clear frame on disconnect or application exit:

```csharp
SpacePilotLcd.Clear(_lcdDevice);
```

`Clear` transmits the suppressor report followed by 8 pages of all-zero column bytes.

---

## Known Limitations

- The suppressor must be re-sent periodically (or before every write) if the application keeps the connection open for extended periods — the firmware may re-enable its own refresh after ~30 seconds.
- The GDI `TextRenderer` API requires `System.Windows.Forms` and a `net10.0-windows` TFM; it is not available on non-Windows targets.
- Color emoji are rendered as monochrome glyphs (the display is 1-bit).
