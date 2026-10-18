using Crayons.Core.Assets;

namespace Crayons.App.Rendering;

/// <summary>
/// Loads and caches images from the original install (loose .ASL/.BMP files, EXE form
/// pictures, EXE icons) as 32-bpp DIB surfaces ready for GDI BitBlt.
/// </summary>
public sealed class ImageCache : IDisposable
{
    readonly AssetStore _store;
    readonly Dictionary<string, DibSurface?> _images = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, NeResources> _exes = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, DibImage?> _icons = new(StringComparer.OrdinalIgnoreCase);

    public ImageCache(AssetStore store) => _store = store;

    public AssetStore Store => _store;

    /// <summary>A bitmap file from the install, e.g. "A1.ASL" or "ART\\CB0111.BMP".</summary>
    public DibSurface File(string relative) =>
        TryFile(relative) ?? throw new FileNotFoundException($"Missing original asset {relative}", _store.PathOf(relative));

    /// <summary>A bitmap file from the install, or null when the file is missing.</summary>
    public DibSurface? TryFile(string relative)
    {
        if (!_images.TryGetValue(relative, out var dib))
            _images[relative] = dib = _store.Exists(relative) ? DibSurface.FromBmp(_store.ReadBytes(relative)) : null;
        return dib;
    }

    public NeResources Exe(string exe)
    {
        if (!_exes.TryGetValue(exe, out var res))
            _exes[exe] = res = NeResources.Load(_store.PathOf(exe));
        return res;
    }

    public DfmObject Form(string exe, string formClass) =>
        Exe(exe).Form(formClass) ?? throw new InvalidDataException($"{exe} has no form {formClass}");

    /// <summary>The picture of a TImage component inside a form of one of the original executables.</summary>
    public DibSurface FormPicture(string exe, string formClass, string component)
    {
        var key = $"{exe}|{formClass}|{component}";
        if (!_images.TryGetValue(key, out var dib) || dib == null)
        {
            var data = Form(exe, formClass).Child(component)?.PictureBitmap()
                       ?? throw new InvalidDataException($"{formClass}.{component} has no picture");
            _images[key] = dib = DibSurface.FromBmp(data);
        }
        return dib;
    }

    /// <summary>A decoded icon group (e.g. "ICON_1") from one of the original executables.</summary>
    public DibImage? Icon(string exe, string group)
    {
        var key = $"{exe}|{group}";
        if (!_icons.TryGetValue(key, out var img))
            _icons[key] = img = Exe(exe).Icon(group);
        return img;
    }

    public void Dispose()
    {
        foreach (var b in _images.Values) b?.Dispose();
        _images.Clear();
    }
}
