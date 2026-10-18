using System.Runtime.InteropServices;
using Crayons.Core.Assets;

namespace Crayons.App.Rendering;

/// <summary>
/// Builds Windows cursors from the original's NE cursor images, scaled by an integer
/// factor with nearest-neighbour sampling (hotspot scaled too), via CreateIconIndirect.
/// Monochrome cursors keep their screen-inverting pixels.
/// </summary>
public static class CursorFactory
{
    public static Cursor Create(DibImage img, int hotX, int hotY, int scale)
    {
        scale = Math.Max(1, scale);
        int w = img.Width * scale, h = img.Height * scale;
        int stride = ((w + 15) / 16) * 2; // monochrome bitmap rows are WORD aligned
        var info = new Gdi.ICONINFO { fIcon = false, xHotspot = hotX * scale, yHotspot = hotY * scale };
        IntPtr hMask, hColor = IntPtr.Zero;
        if (img.IsMonochrome)
        {
            // AND mask on top, XOR mask below; XOR bit = 1 where the cursor colour is white.
            var bits = new byte[stride * h * 2];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y / scale) * img.Width + x / scale;
                    byte bit = (byte)(0x80 >> (x & 7));
                    if (img.And[i]) bits[y * stride + (x >> 3)] |= bit;
                    if ((img.Xor[i] & 0xffffff) != 0) bits[(y + h) * stride + (x >> 3)] |= bit;
                }
            hMask = Gdi.CreateBitmap(w, h * 2, 1, 1, bits);
        }
        else
        {
            var mask = new byte[stride * h];
            var colour = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y / scale) * img.Width + x / scale;
                    if (img.And[i]) mask[y * stride + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                    int c = img.Xor[i];
                    int o = (y * w + x) * 4;
                    colour[o] = (byte)c; colour[o + 1] = (byte)(c >> 8); colour[o + 2] = (byte)(c >> 16);
                }
            hMask = Gdi.CreateBitmap(w, h, 1, 1, mask);
            hColor = Gdi.CreateBitmap(w, h, 1, 32, colour);
        }
        info.hbmMask = hMask;
        info.hbmColor = hColor;
        IntPtr hCursor = Gdi.CreateIconIndirect(ref info);
        Gdi.DeleteObject(hMask);
        if (hColor != IntPtr.Zero) Gdi.DeleteObject(hColor);
        if (hCursor == IntPtr.Zero) throw new ExternalException("CreateIconIndirect failed");
        return new Cursor(hCursor);
    }
}
