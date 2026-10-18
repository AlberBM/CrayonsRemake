using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.App.Tools;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.App.Studio;

/// <summary>
/// Draws the studio's form art onto the 640x480 form surface, the way FormPaint and its
/// helpers did (shell.md §3, tools-lines.md Q9, spec §8.2). Bitmaps come from the user's install.
/// </summary>
public sealed class StudioArt
{
    readonly ImageCache _images;
    readonly LevelDefinition _level;

    public StudioArt(ImageCache images, LevelDefinition level)
    {
        _images = images;
        _level = level;
    }

    DibSurface? Img(string name) => _images.TryFile(name.EndsWith(".ASL", StringComparison.OrdinalIgnoreCase) ? name : name + ".ASL");

    /// <summary>FormPaint steps 1-8 and 10: black fills, frame strips, palette, banner, column, shade grid, panels.</summary>
    public void FormPaint(DibSurface form, StudioState s)
    {
        int black = ColorTable.Get(ColorTable.Black);
        Gdi.FillRect(form.Hdc, 0, 0, 640, 150, black);
        Gdi.FillRect(form.Hdc, 0, 0, 100, 480, black);
        form.Blit(Img("A920"), 0, 0);
        form.Blit(Img("A1"), new Rectangle(0, 0, 81, 179), 0, 0);
        form.Blit(Img("A919"), 80, 0);
        form.Blit(Img("A911A"), Hotspots.CanvasW - 60, 0);
        ClearColumn(form);
        DrawShadeGrid(form, s);
        DrawPanels(form, s);
    }

    /// <summary>Clears the modifier column from A920 (source (0,180) to (0,180), 81 wide).</summary>
    public void ClearColumn(DibSurface form) => form.Blit(Img("A920"), new Rectangle(0, 180, 81, 768), 0, 180);

    /// <summary>Shade grid: A920 backdrop strip, 2 x 5 ellipses (pen #9B9B9B), X marker on the selected shade.</summary>
    public void DrawShadeGrid(DibSurface form, StudioState s)
    {
        form.Blit(Img("A920"), new Rectangle(0, 179, 81, 34), 0, 179);
        IntPtr hdc = form.Hdc;
        using (var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, ColorTable.ColorRef(ColorTable.Grey))))
        {
            for (int i = 0; i < 5; i++)
            {
                using (Gdi.Use(hdc, Gdi.CreateSolidBrush(ColorTable.ColorRef(s.ColorIndex * 10 + i))))
                    Gdi.Ellipse(hdc, 16 * i, 180, 16 * i + 16, 196);
                using (Gdi.Use(hdc, Gdi.CreateSolidBrush(ColorTable.ColorRef(s.ColorIndex * 10 + i + 5))))
                    Gdi.Ellipse(hdc, 16 * i, 196, 16 * i + 16, 212);
            }
        }
        int markRgb = ColorTable.Get(s.ColorIndex == 9 ? ColorTable.Yellow : ColorTable.Red);
        int l = 16 * (s.ShadeIndex % 5), t = s.ShadeIndex < 5 ? 180 : 196, r = l + 16, b = t + 16;
        Gdi.FrameRect(hdc, l, t, r, b, markRgb);
        // R8 (Q7): both diagonals with LineTo semantics (end pixel excluded): (l,t)->(r,b) and (r,t)->(l,b).
        foreach (var (x0, y0, x1, y1) in LinesMath.ShadeMarkerDiagonals(l, t))
            Gdi.Line(hdc, x0, y0, x1, y1, markRgb);
        // The original's grid routine also recomputes the colour and redraws the paint-can panel (shell.md §3.3).
        if (s.IsPanelVisible(PanelId.PaintPatterns) && _level.DrawnPanels.Contains(PanelId.PaintPatterns))
            DrawPanel(form, s, Panels.Get(PanelId.PaintPatterns));
    }

    /// <summary>Every visible (and, for this level, drawn) panel, clipped to 79 px wide.</summary>
    public void DrawPanels(DibSurface form, StudioState s)
    {
        foreach (var p in Panels.All)
        {
            if (!s.IsPanelVisible(p.Id) || !_level.DrawnPanels.Contains(p.Id)) continue;
            DrawPanel(form, s, p);
        }
    }

    void DrawPanel(DibSurface form, StudioState s, PanelDef p)
    {
        if (p.Art(s) is not string file || Img(file) is not { } bmp) return;
        form.Blit(bmp, new Rectangle(0, 0, 79, bmp.Height), 0, p.RowY);
        if (p.Id == PanelId.PaintPatterns) DrawPaintCanPreview(form, s);
    }

    /// <summary>
    /// The Spilled Paint Can preview: the current fill style inside the 26-point splat polygon.
    /// Brush styles 0..7 (spec §9.1 R2): one Polygon with the style brush and a 1-px pen in the
    /// current colour, hatches with a transparent background (the art shows through), style 0
    /// (null brush) as the outline only. Pattern styles 8..15 fill the polygon's region with no
    /// outline. Gradients are drawn over the polygon's bounding box and pictures blitted at the swatch origin,
    /// clipped to the polygon, with no outline. Styles 0..15 and the pictures were matched pixel for
    /// pixel against the runtime screenshots.
    /// </summary>
    public void DrawPaintCanPreview(DibSurface form, StudioState s)
    {
        var pts = PaintMath.PaintCanPolygon.Select(p => new Gdi.POINT(p.X, p.Y)).ToArray();
        int x = PaintMath.PaintCanSwatchX, y = PaintMath.PaintCanSwatchY, n = PaintMath.SwatchSize;
        IntPtr hdc = form.Hdc;
        if (PaintMath.BrushKind(s.FillPattern) is FillBrushKind.Gradient or FillBrushKind.Picture)
        {
            IntPtr rgn = Gdi.CreatePolygonRgn(pts, pts.Length, Gdi.ALTERNATE);
            // Gradients run over the polygon's bounding box (15,299)-(65,325); pictures tile from the swatch origin.
            var gbox = PaintMath.BrushKind(s.FillPattern) == FillBrushKind.Gradient ? PaintMath.PaintCanBounds : (x, y, x + n, y + n);
            FillRenderer.FillStyled(hdc, rgn, s.FillPattern, s.CurrentColor, s.ColorIndex, gbox, _images.TryFile);
            Gdi.DeleteObject(rgn);
            return;
        }
        using var fb = FillBrush.Create(s.FillPattern, s.CurrentColor, _images.TryFile);
        if (s.FillPattern >= 8)
        {
            // Pattern styles 8..15: the polygon's region filled opaquely with the pattern, no outline.
            // The brush origin stays at the form's (0,0). Matched pixel for pixel to the original.
            IntPtr prgn = Gdi.CreatePolygonRgn(pts, pts.Length, Gdi.ALTERNATE);
            FillRenderer.Clipped(hdc, prgn, () =>
            {
                using (Gdi.Use(hdc, fb.Handle, delete: false))
                    Gdi.PatBlt(hdc, x, y, n, n, 0x00F00021 /*PATCOPY*/);
            });
            Gdi.DeleteObject(prgn);
            return;
        }
        int oldMode = Gdi.SetBkMode(hdc, fb.Transparent ? Gdi.TRANSPARENT : Gdi.OPAQUE);
        // The brush origin stays at the form's (0,0), as observed (the horizontal hatch rows are at y = 3 mod 8).
        using (Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, Gdi.Rgb(s.CurrentColor))))
        using (Gdi.Use(hdc, fb.Handle, delete: false))
            Gdi.Polygon(hdc, pts, pts.Length);
        Gdi.SetBkMode(hdc, oldMode);
    }

    /// <summary>The click tail: column, every enabled panel, then the shade grid.</summary>
    public void RedrawColumn(DibSurface form, StudioState s)
    {
        ClearColumn(form);
        DrawPanels(form, s);
        DrawShadeGrid(form, s);
    }

    /// <summary>The A922 strip at (100,50) and, for slot n &lt; 11, the red option triangle.</summary>
    public void DrawOptionStrip(DibSurface form, int slot)
    {
        form.Blit(Img("A922"), 100, 50);
        if (slot >= 11) return;
        IntPtr hdc = form.Hdc;
        uint red = ColorTable.ColorRef(ColorTable.Red);
        using var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, red));
        using var br = Gdi.Use(hdc, Gdi.CreateSolidBrush(red));
        var pts = new[] { new Gdi.POINT(120 + 40 * slot, 51), new Gdi.POINT(100 + 40 * slot, 59), new Gdi.POINT(140 + 40 * slot, 59) };
        Gdi.Polygon(hdc, pts, pts.Length);
    }

    /// <summary>One frame of the options-row animation: sheet row (0, frame*41), 400x40, to (100,10).</summary>
    public void DrawOptionsFrame(DibSurface form, string sheet, int frame) =>
        form.Blit(Img(sheet), new Rectangle(0, frame * 41, 400, 40), 100, 10);
}
