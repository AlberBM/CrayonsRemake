using Crayons.Core.Assets;

namespace Crayons.Tests;

public class AssetStoreTests
{
    [Fact]
    public void Sniff_RecognizesFormats()
    {
        Assert.Equal(Riff.Kind.Bitmap, Riff.Sniff("BM\0\0"u8));
        Assert.Equal(Riff.Kind.Wave, Riff.Sniff("RIFF\0\0\0\0WAVEfmt "u8));
        Assert.Equal(Riff.Kind.Midi, Riff.Sniff("MThd"u8));
        Assert.Equal(Riff.Kind.Text, Riff.Sniff("\r\nCut and Paste"u8));
    }

    [Fact]
    public void Constructor_RejectsFolderWithoutInstall()
    {
        var empty = Directory.CreateTempSubdirectory().FullName;
        Assert.Throws<DirectoryNotFoundException>(() => new AssetStore(empty));
    }

    [SkippableFact]
    public void RealInstall_HasExpectedFormats()
    {
        var root = AssetStore.FindInstall();
        Skip.If(root == null, "No Crayons install on this machine");
        var store = new AssetStore(root!);
        Assert.Equal(Riff.Kind.Bitmap, Riff.Sniff(store.ReadBytes("A1.ASL")));
        Assert.Equal(Riff.Kind.Wave, Riff.Sniff(store.ReadBytes(Path.Combine("SND", "SND1.ASL"))));
        Assert.Equal(Riff.Kind.Midi, Riff.Sniff(store.ReadBytes(Path.Combine("SND", "PC1.MID"))));
        Assert.Equal(20, store.List("SND", "PC*.MID").Count);
    }
}
