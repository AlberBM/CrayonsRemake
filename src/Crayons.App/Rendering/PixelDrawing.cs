namespace Crayons.App.Rendering;

/// <summary>Pixel-exact blit helpers for DIB surfaces.</summary>
public static class PixelDrawing
{
    /// <summary>Draws the whole image 1:1 at (x, y).</summary>
    public static void Blit(this DibSurface dst, DibSurface? img, int x, int y)
    {
        if (img != null) dst.DrawPart(img, x, y, 0, 0, img.Width, img.Height);
    }

    /// <summary>Draws a source rectangle of a sprite sheet 1:1 at (x, y), clipped to the sheet.</summary>
    public static void Blit(this DibSurface dst, DibSurface? sheet, Rectangle src, int x, int y)
    {
        if (sheet != null) dst.DrawPart(sheet, x, y, src.X, src.Y, src.Width, src.Height);
    }
}
