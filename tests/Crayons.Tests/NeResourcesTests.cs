using Crayons.Core.Assets;

namespace Crayons.Tests;

public class NeResourcesTests
{
    static AssetStore? Store()
    {
        var root = AssetStore.FindInstall();
        return root == null ? null : new AssetStore(root);
    }

    [SkippableFact]
    public void LauncherForms_ContainFullScreenPictures()
    {
        var store = Store();
        Skip.If(store == null, "No Crayons install on this machine");
        var res = NeResources.Load(store!.PathOf("CRAYONST.EXE"));
        foreach (var formName in new[] { "TAGEPICK", "TSPLASH" })
        {
            var form = res.Form(formName);
            Assert.NotNull(form);
            var bmp = form!.Child("Image1")!.PictureBitmap();
            Assert.NotNull(bmp);
            Assert.Equal(Riff.Kind.Bitmap, Riff.Sniff(bmp));
        }
        var agepick = res.Form("TAGEPICK")!;
        Assert.Equal((79, 279, 170, 161), agepick.Child("Image2")!.Bounds);
    }

    [SkippableFact]
    public void MainProgram_HasAllDialogFormsAndCursors()
    {
        var store = Store();
        Skip.If(store == null, "No Crayons install on this machine");
        var res = NeResources.Load(store!.PathOf("CRAYON2.EXE"));
        foreach (var f in new[] { "TMAIN", "TJUKEBOX", "TACTIVITY", "TROTATOR", "TTEXTINPT", "TFONTFORM", "TFILLSTYLES", "TTHUMBS", "TABOUTME", "TORDERS" })
            Assert.NotNull(res.Form(f));
        var groups = res.Entries.Where(e => e.Type == NeResources.RtGroupCursor.ToString()).ToList();
        Assert.Equal(42, groups.Count);
        var cur = res.CursorFile(groups[0].Name);
        Assert.NotNull(cur);
        Assert.Equal(2, BitConverter.ToUInt16(cur!, 2)); // .cur type
    }

    [SkippableFact]
    public void ToolbarIcons_LoadAsIcoFiles()
    {
        var store = Store();
        Skip.If(store == null, "No Crayons install on this machine");
        var res = NeResources.Load(store!.PathOf("CRAYON2.EXE"));
        foreach (var n in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12 })
        {
            var ico = res.IconFile($"ICON_{n}");
            Assert.NotNull(ico);
            using var icon = new System.Drawing.Icon(new MemoryStream(ico!));
            Assert.Equal(32, icon.Width);
        }
    }
}
