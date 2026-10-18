using Crayons.App.Rendering;
using Crayons.Core.Content;

namespace Crayons.App.Tools;

/// <summary>Clip-art stamping (mode 0x33) and the Magic Color reveal (mode 0x50), content-files.md §4 and §6.</summary>
public static class PictureTools
{
    /// <summary>
    /// Mode 0x33: on the left press snapshot into A and load the clip art; on press, every move and the
    /// release, repaint the previous rectangle from A, compose A's background with the mask (SRCAND) and
    /// the image (SRCPAINT) in a work bitmap and blit it centred on the pointer. The release plays SND57
    /// unless a right press cancelled the drop. The mode stays 0x33.
    /// </summary>
    public sealed class Stamp : ITool
    {
        DibSurface? _source, _work;
        int _hw, _hh, _orientation;
        (int X, int Y)? _last;

        public void Down(ToolContext c, int x, int y)
        {
            var s = c.State;
            c.SnapshotCanvas();
            s.CancelPlace = false;
            Dispose();
            _source = c.LoadPath(s.PicturePath);
            _orientation = s.PictureOrientation;
            (_hw, _hh) = PictureKinds.StampHalf(_orientation);
            _work = new DibSurface(2 * _hw, 2 * _hh);
            Place(c, x, y);
        }

        public void Move(ToolContext c, int x, int y) => Place(c, x, y);

        public void Up(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (s.CancelPlace)
            {
                RestoreLast(c);
                s.CancelPlace = false;
            }
            else
            {
                Place(c, x, y);
                c.PlaySound(57);
            }
            _last = null;
            Dispose();
        }

        void RestoreLast(ToolContext c)
        {
            if (_last is not var (lx, ly) || c.Backing == null) return;
            Gdi.BitBlt(c.Canvas.Hdc, lx, ly, 2 * _hw, 2 * _hh, c.Backing.Hdc, lx, ly, Gdi.SRCCOPY);
        }

        void Place(ToolContext c, int x, int y)
        {
            if (_work == null || c.Backing == null) return;
            RestoreLast(c);
            int px = x - _hw, py = y - _hh, w = 2 * _hw, h = 2 * _hh;
            Gdi.BitBlt(_work.Hdc, 0, 0, w, h, c.Backing.Hdc, px, py, Gdi.SRCCOPY);
            if (_source != null)
            {
                // H files: image in the top 200x100, mask below; V files: image left 100x200, mask right.
                int mx = _orientation == 3 ? 100 : 0, my = _orientation == 3 ? 0 : 100;
                Gdi.BitBlt(_work.Hdc, 0, 0, w, h, _source.Hdc, mx, my, Gdi.SRCAND);
                Gdi.BitBlt(_work.Hdc, 0, 0, w, h, _source.Hdc, 0, 0, Gdi.SRCPAINT);
            }
            Gdi.BitBlt(c.Canvas.Hdc, px, py, w, h, _work.Hdc, 0, 0, Gdi.SRCCOPY);
            _last = (px, py);
        }

        void Dispose()
        {
            _source?.Dispose();
            _work?.Dispose();
            _source = _work = null;
        }
    }

    /// <summary>
    /// Mode 0x50, Magic Color: a click (SND5 from the mode table) on one of the 48 key colours flood-fills
    /// that colour (FLOODFILLSURFACE) with its table colour; any other colour does nothing.
    /// </summary>
    public sealed class MagicColor : ITool
    {
        public void Down(ToolContext c, int x, int y)
        {
            int key = c.Canvas.Surface.GetRgb(x, y);
            if (key < 0 || MagicColorKeys.FillFor(key) is not int fill) return;
            IntPtr hdc = c.Canvas.Hdc;
            using (Gdi.Use(hdc, Gdi.CreateSolidBrush(Gdi.Rgb(fill))))
                Gdi.ExtFloodFill(hdc, x, y, Gdi.Rgb(key), Gdi.FLOODFILLSURFACE);
        }

        public void Move(ToolContext c, int x, int y) { }
    }
}
