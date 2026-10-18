namespace Crayons.App.Tools;

/// <summary>Mode → tool. Modes without an entry do nothing on the canvas yet (later phases).</summary>
public static class ToolSet
{
    /// <summary>A fresh set of tools (some tools keep per-stroke state, so each studio gets its own).</summary>
    public static IReadOnlyDictionary<int, ITool> Create()
    {
        var shapes = new ShapesTools.Shape();
        var shaded = new ShapesTools.Shaded3D();
        var nib = new PaintTools.Nib();
        var parallelogram = new ShapesTools.Parallelogram();
        var styled = new TextTools.StyledText();
        var face = new ChangeItTools.Face();
        var selection = new ChangeItTools.Selection();
        return new Dictionary<int, ITool>
        {
            // Lines
            [1] = new LinesTools.Crayon(),
            [0x17] = new LinesTools.Radial(),
            [0x0A] = new LinesTools.Chalk(),
            [5] = new LinesTools.CrazyPen(),
            [2] = new LinesTools.Corner(),
            [0x0B] = new LinesTools.Symmetry(),
            [0x13] = new LinesTools.Caterpillar(),
            [0x0C] = new LinesTools.ThickThin(),
            [0x0D] = new LinesTools.ThickThin(),
            [0x26] = new LinesTools.StraightLine(),
            // Paint
            [0x16] = new PaintTools.Dripping(),
            [0x15] = nib,
            [0x08] = nib,
            [0x0F] = nib,
            [0x0E] = nib,
            [0x3A] = new PaintTools.FillBucket(),
            [0x19] = new PaintTools.Roller(),
            [0x10] = new PaintTools.Splatter(),
            [0x09] = new PaintTools.Spray(),
            [0x2A] = new PaintTools.ThrowRugs(),
            // Wacky
            [7] = new WackyTools.MiniShapes(),
            [6] = new WackyTools.Spider(),
            [3] = new WackyTools.Crosshair(),
            [0x1B] = new WackyTools.ShadedBubbles(),
            [0x1A] = new WackyTools.OutlineBubbles(),
            [0x11] = new WackyTools.Ornaments(),
            [0x32] = new WackyTools.MrBrush(),
            [0x30] = new WackyTools.Train(),
            [0x38] = new WackyTools.Hair(),
            [4] = new WackyTools.Grass(),
            // Shapes
            [0x21] = shapes, [0x1F] = shapes, [0x22] = shapes, [0x20] = shapes, [0x23] = shapes,
            [0x29] = shapes, [0x28] = shapes, [0x25] = shapes, [0x24] = shapes,
            [0x36] = parallelogram, [0x37] = parallelogram,
            // 3D
            [0x2C] = shaded, [0x2D] = shaded, [0x2E] = shaded,
            [0x2B] = new ShapesTools.Torus(),
            [0x3B] = new ShapesTools.Line3D(),
            [0x3C] = new ShapesTools.Cube(),
            [0x3D] = new ShapesTools.Jumble(),
            [0x3E] = new ShapesTools.Smear(),
            // Text (Older) and ABCD
            [0x4B] = styled, [0x4C] = styled, [0x4D] = styled, [0x4E] = styled,
            [0x4F] = new TextTools.RotatedText(),
            [0x51] = new TextTools.Abcd(),
            // Eraser
            [0x2F] = new EraserTools.Explode(),
            [0x39] = new EraserTools.Square(),
            // Change It
            [0x3F] = face, [0x40] = face,
            [0x41] = new ChangeItTools.Zoom(),
            [0x42] = new ChangeItTools.Reverse(),
            [0x43] = new ChangeItTools.Invert(),
            [0x44] = new ChangeItTools.EyeDropper(),
            [0x45] = selection, [0x46] = selection, [0x47] = selection,
            [0x48] = selection, [0x49] = selection, [0x4A] = selection,
            // Pictures and Magic Color
            [0x33] = new PictureTools.Stamp(),
            [0x50] = new PictureTools.MagicColor(),
        };
    }
}
