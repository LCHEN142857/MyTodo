using MyToDo.App.Settings;

namespace MyToDo.App.Services;

public interface IDesktopWindowService
{
    void AttachToDesktop(IntPtr windowHandle);

    void SetTopmost(IntPtr windowHandle, bool isTopmost);

    AppSettings EnsureVisible(AppSettings settings, IReadOnlyList<DisplayBounds> displays);
}

public readonly record struct DisplayBounds(double Left, double Top, double Width, double Height, bool IsPrimary = false)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}
