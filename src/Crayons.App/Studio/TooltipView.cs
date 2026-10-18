using Crayons.App.Rendering;
using Crayons.Core.Studio;

namespace Crayons.App.Studio;

/// <summary>
/// The yellow right-click caption panel (the original Panel1 with Label1-3), spec §8.5:
/// SYSTEM_FONT black labels stacked from y = 0 at left 2, a 1-px black border,
/// height = sum of label heights + 2, width = widest label + 10, placed at (X, Y+16)
/// and shifted left so it ends before canvasW.
/// </summary>
public sealed class TooltipView : IDisposable
{
    public DibSurface? Surface { get; private set; }
    public int Left { get; private set; }
    public int Top { get; private set; }

    /// <summary>Splits a caption on '|' into at most three labels.</summary>
    public static string[] Labels(string caption) => caption.Split('|').Take(3).ToArray();

    /// <summary>Panel left edge: X, or canvasW - width when X + width &gt;= canvasW.</summary>
    public static int PlaceLeft(int x, int width) => x + width >= Hotspots.CanvasW ? Hotspots.CanvasW - width : x;

    public void Build(string caption, int x, int y)
    {
        Dispose();
        var labels = Labels(caption);
        IntPtr font = Gdi.GetStockObject(Gdi.SYSTEM_FONT);
        var sizes = new Gdi.SIZE[labels.Length];
        using (var probe = new DibSurface(1, 1))
        using (Gdi.Use(probe.Hdc, font, delete: false))
            for (int i = 0; i < labels.Length; i++)
            {
                // An empty label still has the font's height (as an auto-sized label measures " ").
                string m = labels[i].Length == 0 ? " " : labels[i];
                Gdi.GetTextExtentPoint32(probe.Hdc, m, m.Length, out sizes[i]);
                if (labels[i].Length == 0) sizes[i].cx = 0;
            }
        int h = sizes.Sum(s => s.cy) + 2;
        int w = sizes.Max(s => s.cx) + 10;
        var surf = new DibSurface(w, h, 0xFFFF00);
        Gdi.FrameRect(surf.Hdc, 0, 0, w, h, 0x000000);
        using (Gdi.Use(surf.Hdc, font, delete: false))
        {
            Gdi.SetBkMode(surf.Hdc, Gdi.TRANSPARENT);
            Gdi.SetTextColor(surf.Hdc, 0);
            int ty = 1;
            for (int i = 0; i < labels.Length; i++)
            {
                Gdi.TextOut(surf.Hdc, 1 + 2, ty, labels[i], labels[i].Length);
                ty += sizes[i].cy;
            }
        }
        Surface = surf;
        Left = PlaceLeft(x, w);
        Top = y + 16;
    }

    public void Draw(DibSurface frame)
    {
        if (Surface != null) frame.Blit(Surface, Left, Top);
    }

    public void Dispose()
    {
        Surface?.Dispose();
        Surface = null;
    }
}
