using System;

namespace MyToDo.App.Settings;

public sealed record AppSettings(
    double Left,
    double Top,
    double Width,
    double Height,
    double Opacity,
    bool IsTopmost)
{
    public static AppSettings Default { get; } = new(0, 0, 320, 520, 0.92, false);

    public AppSettings Normalize() => this with
    {
        Left = double.IsFinite(Left) ? Left : 0,
        Top = double.IsFinite(Top) ? Top : 0,
        Width = double.IsFinite(Width) ? Math.Max(260, Width) : Default.Width,
        Height = double.IsFinite(Height) ? Math.Max(320, Height) : Default.Height,
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.35, 1) : Default.Opacity
    };
}
