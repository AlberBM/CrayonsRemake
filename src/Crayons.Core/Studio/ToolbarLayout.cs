namespace Crayons.Core.Studio;

/// <summary>Colours used by the procedural toolbar (spec §8.1).</summary>
public enum TabPixel : byte { White, Black, Dark, Light }

/// <summary>
/// The TabSet look reproduced procedurally from the observed pixel map (spec §8.1):
/// rows y = 60 (#A0A0A0) and 61 (#646464), a checkerboard below, and trapezoid tabs
/// hanging from y = 62 to 91. Tab i's top-left edge is at x = 85 + 41i; its left edge
/// moves right by one pixel every 6 rows and its right edge (top x = left + 45) moves
/// left the same way. Left tabs overlap right tabs; the selected tab is drawn last.
/// </summary>
public static class ToolbarLayout
{
    public const int X0 = 82, Y0 = 60, Width = 640 - X0, Height = 32;
    public const int TabPitch = 41, FirstTabX = 85, TabTopWidth = 45;

    public const int DarkRgb = 0x646464, LightRgb = 0xA0A0A0;

    public static int TabLeftAt(int tab, int y) => FirstTabX + TabPitch * tab + (y - 59) / 6;
    public static int TabRightAt(int tab, int y) => FirstTabX + TabPitch * tab + TabTopWidth - (y - 58) / 6;

    /// <summary>Icon position: x = 95 + 40i (i &lt; 7) or 102 + 40i, y = 60 (shell.md §3.5).</summary>
    public static (int X, int Y) IconAt(int tab) => (tab < 7 ? 95 + 40 * tab : 102 + 40 * tab, 60);

    /// <summary>
    /// Horizontal clip for tab i's icon: x in [82 + 41i, 123 + 41i). Observed in the pixel map:
    /// icon pixels at or right of 123 + 41i never appear (the original drew each icon clipped
    /// to its 41-px tab slot). The left bound is inferred (no icon reaches it).
    /// </summary>
    public static (int X0, int X1) IconClip(int tab) => (X0 + TabPitch * tab, X0 + TabPitch * (tab + 1));

    /// <summary>Icons are clipped above the tabs' bottom line: rows y &lt; 91 (observed: the #646464 line at y = 91 stays over icon art).</summary>
    public const int IconClipBottom = 91;

    /// <summary>
    /// Bottom clip (exclusive) for tab i's icon. The selected tab's #A0A0A0 row at y = 90 is drawn over
    /// its icon (spec §9.1 R10, seen on the Change It tab at x 439-441), so that icon stops at y &lt; 90.
    /// </summary>
    public static int IconClipBottomFor(int tab, int selected) => tab == selected ? 90 : IconClipBottom;

    /// <summary>Right-click tab tooltip anchor in frame coordinates: toolbar-local (x + 80, y + 60), i.e. (X - 2, Y) (R9).</summary>
    public static (int X, int Y) TabTooltipAnchor(int frameX, int frameY) => (frameX - X0 + 80, frameY - Y0 + 60);

    public static int Rgb(TabPixel p) => p switch
    {
        TabPixel.White => 0xFFFFFF,
        TabPixel.Black => 0x000000,
        TabPixel.Dark => DarkRgb,
        _ => LightRgb,
    };

    /// <summary>The strip without icons, indexed [y - 60, x - 82].</summary>
    public static TabPixel[,] Render(int tabCount, int selected)
    {
        var px = new TabPixel[Height, Width];
        void Set(int x, int y, TabPixel p)
        {
            if (x >= X0 && x < 640 && y >= Y0 && y < Y0 + Height) px[y - Y0, x - X0] = p;
        }

        for (int y = Y0; y < Y0 + Height; y++)
            for (int x = X0; x < 640; x++)
                Set(x, y, y == 60 ? TabPixel.Light : y == 61 ? TabPixel.Dark : ((x + y) & 1) == 0 ? TabPixel.White : TabPixel.Black);

        void Tab(int i, bool sel)
        {
            int lx = FirstTabX + TabPitch * i;
            if (sel)
            {
                for (int x = lx + 1; x <= lx + 43; x++) Set(x, 60, TabPixel.Black);
                Set(lx + 1, 61, TabPixel.White);
                for (int x = lx + 2; x <= lx + 43; x++) Set(x, 61, TabPixel.Black);
                Set(lx + 44, 61, TabPixel.Light);
            }
            for (int y = 62; y <= 91; y++)
            {
                int l = TabLeftAt(i, y), r = TabRightAt(i, y);
                if (y == 91)
                {
                    for (int x = l; x <= r; x++) Set(x, y, TabPixel.Dark);
                    continue;
                }
                Set(l, y, TabPixel.Dark);
                Set(r, y, TabPixel.Dark);
                for (int x = l + 1; x < r; x++)
                {
                    TabPixel p;
                    if (!sel) p = TabPixel.White;
                    else if (y == 90) p = TabPixel.Light;
                    else if (x == r - 1) p = TabPixel.Light;
                    else if (x == l + 1 && y <= 88) p = TabPixel.White;
                    else p = TabPixel.Black;
                    Set(x, y, p);
                }
            }
        }

        for (int i = tabCount - 1; i >= 0; i--) if (i != selected) Tab(i, false);
        if (selected >= 0 && selected < tabCount) Tab(selected, true);
        return px;
    }
}
