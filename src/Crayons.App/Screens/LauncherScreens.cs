using Crayons.App.Rendering;
using Crayons.Core.Studio;

namespace Crayons.App.Screens;

/// <summary>Title splash shown at startup (original form Tsplash: 637x480 picture, 3000 ms timer).</summary>
public sealed class SplashScreen : GameScreen
{
    const int DurationMs = 3000;
    readonly DibSurface _picture;
    int _elapsed;

    public SplashScreen(GameHost host) : base(host) =>
        _picture = host.Images.FormPicture("CRAYONST.EXE", "TSPLASH", "Image1");

    public override void Render(DibSurface frame)
    {
        frame.Fill(0);
        frame.Blit(_picture, 0, 0);
    }

    public override void Tick(int elapsedMs)
    {
        _elapsed += elapsedMs;
        if (_elapsed >= DurationMs) Host.Show(new AgePickerScreen(Host));
    }

    public override void MouseDown(Point p, MouseButtons button) => Host.Show(new AgePickerScreen(Host));
}

/// <summary>"Welcome!" level picker (original form Tagepick): left figure = Younger, right = Older.</summary>
public sealed class AgePickerScreen : GameScreen
{
    readonly DibSurface _picture;
    readonly Rectangle _younger, _older;

    public AgePickerScreen(GameHost host) : base(host)
    {
        var form = host.Images.Form("CRAYONST.EXE", "TAGEPICK");
        _picture = host.Images.FormPicture("CRAYONST.EXE", "TAGEPICK", "Image1");
        _younger = ToRect(form.Child("Image2")!.Bounds);
        _older = ToRect(form.Child("Image3")!.Bounds);
    }

    static Rectangle ToRect((int X, int Y, int W, int H) b) => new(b.X, b.Y, b.W, b.H);

    public override void Render(DibSurface frame)
    {
        frame.Fill(0);
        frame.Blit(_picture, 0, 0);
    }

    public override Cursor Cursor => Cursors.Hand;

    public override void MouseDown(Point p, MouseButtons button)
    {
        if (_younger.Contains(p)) Host.StartLevel(Level.Younger);
        else if (_older.Contains(p)) Host.StartLevel(Level.Older);
    }
}
