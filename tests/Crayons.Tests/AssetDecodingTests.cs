using Crayons.Core.Assets;
using Crayons.Core.Studio;

namespace Crayons.Tests;

public class AssetDecodingTests
{
    /// <summary>A synthetic 8x2 1-bpp icon DIB (black/white palette) with a checker AND mask.</summary>
    static byte[] SyntheticIcon()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(8); w.Write(4); w.Write((short)1); w.Write((short)1);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        w.Write(0x00000000); w.Write(0x00FFFFFF);               // palette: black, white
        w.Write(new byte[] { 0b1000_0001, 0, 0, 0 });           // XOR bottom row
        w.Write(new byte[] { 0b1111_0000, 0, 0, 0 });           // XOR top row
        w.Write(new byte[] { 0b0000_1111, 0, 0, 0 });           // AND bottom row
        w.Write(new byte[] { 0b1010_1010, 0, 0, 0 });           // AND top row
        return ms.ToArray();
    }

    [Fact]
    public void DibImage_DecodesBottomUpPlanes()
    {
        var img = DibImage.FromIconDib(SyntheticIcon());
        Assert.Equal((8, 2, 1), (img.Width, img.Height, img.BitCount));
        Assert.Equal(0xFFFFFF, img.Xor[0]);   // top row, x = 0
        Assert.Equal(0, img.Xor[4]);
        Assert.Equal(0xFFFFFF, img.Xor[8]);   // bottom row, x = 0
        Assert.Equal(0xFFFFFF, img.Xor[15]);
        Assert.True(img.And[0]);
        Assert.False(img.And[1]);
        Assert.False(img.And[8]);
        Assert.True(img.And[15]);
        // DrawIcon rule (dst AND mask) XOR colour, top row: x=1 opaque white, x=2 inverts, x=4 keeps, x=5 opaque black.
        Assert.Equal(0xFFFFFF, img.Compose(1, 0, 0x123456));
        Assert.Equal(0x123456 ^ 0xFFFFFF, img.Compose(2, 0, 0x123456));
        Assert.Equal(0x123456, img.Compose(4, 0, 0x123456));
        Assert.Equal(0x000000, img.Compose(5, 0, 0x123456));
    }

    [Fact]
    public void WaveDuration_FromHeader()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + 11025); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(11025); w.Write(11025); w.Write((short)1); w.Write((short)8);
        w.Write("data"u8); w.Write(11025); w.Write(new byte[11025]);
        Assert.Equal(1000, Riff.WaveDurationMs(ms.ToArray()));
    }

    static AssetStore? Store()
    {
        var root = AssetStore.FindInstall();
        return root == null ? null : new AssetStore(root);
    }

    [SkippableFact]
    public void TabIcons_DecodeAs32x32()
    {
        var store = Store();
        Skip.If(store == null, "No Crayons install on this machine");
        foreach (var level in new[] { LevelDefinition.Older, LevelDefinition.Younger })
        {
            var res = NeResources.Load(store!.PathOf(level.Exe));
            foreach (var tab in level.Tabs)
            {
                var icon = res.Icon(tab.Icon);
                Assert.NotNull(icon);
                Assert.Equal((32, 32), (icon!.Width, icon.Height));
            }
        }
    }

    [SkippableFact]
    public void EveryRegisteredCursorExists()
    {
        var store = Store();
        Skip.If(store == null, "No Crayons install on this machine");
        var res = NeResources.Load(store!.PathOf("CRAYON2.EXE"));
        for (int i = 1; i <= 37; i++)
        {
            if (CursorRules.ResourceName(i) is not string name) continue;
            var c = res.Cursor(name);
            Assert.True(c != null, $"missing {name}");
            Assert.InRange(c!.Value.HotX, 0, c.Value.Image.Width - 1);
        }
        // Spot checks against the documented hotspots (input.md §7).
        Assert.Equal((15, 15), (res.Cursor("CURSOR_1")!.Value.HotX, res.Cursor("CURSOR_1")!.Value.HotY));
        Assert.Equal((6, 24), (res.Cursor("CURSOR_37")!.Value.HotX, res.Cursor("CURSOR_37")!.Value.HotY));
    }

    [SkippableFact]
    public void Cursors_BuildAtSeveralScales()
    {
        var store = Store();
        Skip.If(store == null, "No Crayons install on this machine");
        var res = NeResources.Load(store!.PathOf("CRAYON2.EXE"));
        foreach (int scale in new[] { 1, 2, 3 })
            foreach (int index in new[] { 1, 2, 12, 14, 36, 37 })
            {
                var c = res.Cursor(CursorRules.ResourceName(index)!)!.Value;
                using var cursor = Crayons.App.Rendering.CursorFactory.Create(c.Image, c.HotX, c.HotY, scale);
                Assert.NotEqual(IntPtr.Zero, cursor.Handle);
                Assert.Equal(new System.Drawing.Point(c.HotX * scale, c.HotY * scale), cursor.HotSpot);
            }
    }

    [Theory]
    [InlineData(20, 50, 20)]
    [InlineData(507, 50, 507)]   // 557 < canvasW: stays
    [InlineData(510, 50, 508)]   // 560 >= canvasW: shifted to canvasW - width
    [InlineData(600, 40, 518)]
    public void TooltipPlacement(int x, int w, int expectedLeft) =>
        Assert.Equal(expectedLeft, Crayons.App.Studio.TooltipView.PlaceLeft(x, w));

    [Fact]
    public void TooltipLabels_SplitOnBarIntoAtMostThree()
    {
        Assert.Equal(new[] { "Text with", "Shadows" }, Crayons.App.Studio.TooltipView.Labels("Text with|Shadows"));
        Assert.Equal(3, Crayons.App.Studio.TooltipView.Labels("a|b|c|d").Length);
    }

    [SkippableFact]
    public void CaptionFiles_HaveTabCaptions()
    {
        var store = Store();
        Skip.If(store == null, "No Crayons install on this machine");
        var older = store!.ReadLines("A938.ASL");
        var younger = store.ReadLines("A939.ASL");
        Assert.StartsWith("Lines", older[LevelDefinition.Older.TabTooltipBase]);
        Assert.StartsWith("Lines", younger[LevelDefinition.Younger.TabTooltipBase]);
        Assert.Equal("Undo", older[109].Trim());
    }
}
