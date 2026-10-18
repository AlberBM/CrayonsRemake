using Crayons.App.Rendering;
using Crayons.Core.Studio;

namespace Crayons.App.Studio;

/// <summary>The main toolbar (the original TabSet) drawn procedurally per spec §8.1, with the EXE's tab icons.</summary>
public sealed class ToolbarView : IDisposable
{
    public DibSurface Surface { get; } = new(ToolbarLayout.Width, ToolbarLayout.Height, 0);

    public void Draw(LevelDefinition level, ImageCache images, int selected)
    {
        var px = ToolbarLayout.Render(level.Tabs.Count, selected);
        Surface.Flush();
        var dst = Surface.Pixels;
        for (int y = 0; y < ToolbarLayout.Height; y++)
            for (int x = 0; x < ToolbarLayout.Width; x++)
                dst[y * ToolbarLayout.Width + x] = 0xff000000u | (uint)ToolbarLayout.Rgb(px[y, x]);
        // Icons come from CRAYON1/CRAYON2.EXE (GROUP_ICON "ICON_n"), drawn with their AND masks
        // and clipped to their tab slot as observed.
        for (int i = 0; i < level.Tabs.Count; i++)
        {
            if (images.Icon(level.Exe, level.Tabs[i].Icon) is not { } icon) continue;
            var (ix, iy) = ToolbarLayout.IconAt(i);
            var (c0, c1) = ToolbarLayout.IconClip(i);
            Surface.DrawIcon(icon, ix - ToolbarLayout.X0, iy - ToolbarLayout.Y0, c0 - ToolbarLayout.X0, c1 - ToolbarLayout.X0, ToolbarLayout.IconClipBottomFor(i, selected) - ToolbarLayout.Y0);
        }
    }

    public void Dispose() => Surface.Dispose();
}
