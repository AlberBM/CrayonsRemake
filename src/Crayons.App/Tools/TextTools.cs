using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// The Older Text tab (modes 0x4B-0x4F) and ABCD typing (mode 0x51), behavior/tools-text-edit.md §2-3,
/// spec §8.6. Text is drawn with GDI TextOut and CreateFont, transparent, without anti-aliasing.
/// </summary>
public static class TextTools
{
    /// <summary>Shade 9 of the current colour group (the shadow / depth colour).</summary>
    static int Shade9(StudioState s) => ColorTable.Get(s.ColorIndex * 10 + 9);

    /// <summary>Width of <paramref name="text"/> in CreateFont(<paramref name="height"/>, face).</summary>
    public static int TextWidth(IntPtr hdc, string face, int height, string text)
    {
        using (Gdi.Use(hdc, Gdi.Font(height, face)))
        {
            Gdi.GetTextExtentPoint32(hdc, text, text.Length, out var sz);
            return sz.cx;
        }
    }

    static void Out(IntPtr hdc, string face, int height, int escapement, uint align, int x, int y, int rgb, string text)
    {
        using (Gdi.Use(hdc, Gdi.Font(height, face, escapement)))
        {
            Gdi.SetTextAlign(hdc, align);
            Gdi.SetTextColor(hdc, Gdi.Rgb(rgb));
            Gdi.TextOut(hdc, x, y, text, text.Length);
        }
    }

    /// <summary>
    /// Modes 0x4B-0x4E: drag a box; on release (X and Y changed) snapshot, show TextInpt, restore the
    /// snapshot, play the style sound and draw the buffer fitted to the box width.
    /// </summary>
    public sealed class StyledText : BoxTool
    {
        protected override void Action(ToolContext c, Box box)
        {
            int mode = c.State.Mode;
            c.SnapshotCanvas();
            c.AskText(() =>
            {
                c.RestoreCanvas();
                if (ModeSounds.TextSound(mode) is int snd) c.PlaySound(snd);
                Draw(c, mode, box);
            });
        }

        /// <summary>Draws the text buffer in one of the four box styles. Returns the fitted size (0 when empty).</summary>
        public static int Draw(ToolContext c, int mode, Box box)
        {
            var s = c.State;
            string text = s.TextBuffer;
            if (text.Length == 0) return 0;
            IntPtr hdc = c.Canvas.Hdc;
            string face = s.ShadowTextFontName;
            int w = box.Width;
            int size = TextMath.FitSize(sz => TextWidth(hdc, face, -sz, text), w, TextMath.FitGap(mode));
            int colour = s.CurrentColor, shade = Shade9(s);
            int x0 = box.L, y0 = box.T;
            Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
            switch (mode)
            {
                case TextMath.Plain:
                    s.RotationAngle = size; // quirk: the plain style also overwrites the rotation angle
                    Out(hdc, face, -size, 0, Gdi.TA_LEFT, x0, y0, colour, text);
                    break;
                case TextMath.Shadow:
                {
                    var (dx, dy) = TextMath.ShadowOffset(s.Shadow, size / 10);
                    Out(hdc, face, -size, 0, Gdi.TA_LEFT, x0 + dx, y0 + dy, shade, text);
                    Out(hdc, face, -size, 0, Gdi.TA_LEFT, x0, y0, colour, text);
                    break;
                }
                case TextMath.ThreeD:
                {
                    var (layers, faceAt) = TextMath.ThreeDLayout(s.Shadow, size, x0, y0);
                    foreach (var (lx, ly) in layers) Out(hdc, face, -size, 0, Gdi.TA_LEFT, lx, ly, shade, text);
                    Out(hdc, face, -size, 0, Gdi.TA_LEFT, faceAt.X, faceAt.Y, colour, text);
                    break;
                }
                case TextMath.Perspective:
                {
                    int cx = (box.L + box.R) / 2;
                    var (copies, top) = TextMath.PerspectiveLayout(size, w, box.B);
                    const uint align = Gdi.TA_CENTER | Gdi.TA_BASELINE;
                    foreach (var (h, baseline) in copies) Out(hdc, face, h, 0, align, cx, baseline, shade, text);
                    Out(hdc, face, top.Height, 0, align, cx, top.Baseline, colour, text);
                    break;
                }
            }
            Gdi.SetTextAlign(hdc, Gdi.TA_LEFT);
            Gdi.SetBkMode(hdc, Gdi.OPAQUE);
            return size;
        }
    }

    /// <summary>Mode 0x4F, rotated text: on release, snapshot, show the Rotator, restore and draw (no sound).</summary>
    public sealed class RotatedText : ITool
    {
        public void Down(ToolContext c, int x, int y) { }
        public void Move(ToolContext c, int x, int y) { }

        public void Up(ToolContext c, int x, int y)
        {
            c.SnapshotCanvas();
            c.AskRotation(() =>
            {
                c.RestoreCanvas();
                Draw(c, x, y);
            });
        }

        /// <summary>CreateFont(-round(t*7*96/72), escapement = orientation = angle*10), centre/baseline at the click.</summary>
        public static void Draw(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (s.TextBuffer.Length == 0) return;
            IntPtr hdc = c.Canvas.Hdc;
            Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
            Out(hdc, s.ShadowTextFontName, -TextMath.ThicknessFontSize(s.Thickness), s.RotationAngle * 10,
                Gdi.TA_CENTER | Gdi.TA_BASELINE, x, y, s.CurrentColor, s.TextBuffer);
            Gdi.SetTextAlign(hdc, Gdi.TA_LEFT);
            Gdi.SetBkMode(hdc, Gdi.OPAQUE);
        }
    }

    /// <summary>
    /// Mode 0x51, ABCD: a click snapshots, sets the origin (X, Y-16), clears the buffer, starts typing
    /// and draws the caret. Keys come from the studio (<see cref="Key"/>); <see cref="Commit"/> ends typing.
    /// </summary>
    public sealed class Abcd : ITool
    {
        public void Down(ToolContext c, int x, int y)
        {
            var s = c.State;
            c.SnapshotCanvas();
            s.TextOriginX = x;
            s.TextOriginY = y - 16;
            s.TextBuffer = "";
            s.TypingActive = true;
            DrawCaret(c, 0);
        }

        public void Move(ToolContext c, int x, int y) { }

        static int FontHeight(StudioState s) => -TextMath.ThicknessFontSize(s.Thickness);

        static void DrawCaret(ToolContext c, int textWidth)
        {
            var s = c.State;
            int x = s.TextOriginX + textWidth, y = s.TextOriginY;
            Gdi.FillRect(c.Canvas.Hdc, x, y, x + s.Thickness, y + 9 * s.Thickness, s.CurrentColor);
        }

        /// <summary>Draws the buffer at the origin and returns its width.</summary>
        static int DrawText(ToolContext c)
        {
            var s = c.State;
            IntPtr hdc = c.Canvas.Hdc;
            if (s.TextBuffer.Length == 0) return 0;
            Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
            Out(hdc, s.TextFontName, FontHeight(s), 0, Gdi.TA_LEFT, s.TextOriginX, s.TextOriginY, s.CurrentColor, s.TextBuffer);
            Gdi.SetBkMode(hdc, Gdi.OPAQUE);
            return TextWidth(hdc, s.TextFontName, FontHeight(s), s.TextBuffer);
        }

        /// <summary>One typed character (FormKeyPress): restore, apply, redraw text and caret.</summary>
        public static void Key(ToolContext c, char key)
        {
            var s = c.State;
            if (!s.TypingActive) return;
            c.RestoreCanvas();
            string text = s.TextBuffer;
            var result = TextMath.ApplyKey(ref text, key);
            s.TextBuffer = text;
            if (result == TextMath.KeyResult.Commit)
            {
                DrawText(c);
                s.TypingActive = false;
                return;
            }
            DrawCaret(c, DrawText(c));
        }

        /// <summary>Commit (FUN_1008_b990): restore the snapshot and draw the text without the caret.</summary>
        public static void Commit(ToolContext c)
        {
            if (!c.State.TypingActive) return;
            c.RestoreCanvas();
            DrawText(c);
            c.State.TypingActive = false;
        }
    }
}
