using MyToDo.App.Settings;

namespace MyToDo.App.Services;

public interface IDesktopWindowService
{
    IReadOnlyList<DisplayBounds> GetDisplayBounds();

    void DetachFromDesktop(IntPtr windowHandle);

    void AttachToDesktop(IntPtr windowHandle);

    void SetTopmost(IntPtr windowHandle, bool isTopmost);

    void ActivateWindow(IntPtr windowHandle);

    AppSettings EnsureVisible(AppSettings settings, IReadOnlyList<DisplayBounds> displays);
}

public readonly record struct DisplayBounds(double Left, double Top, double Width, double Height, bool IsPrimary = false)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public static DisplayBounds FromPixels(double left, double top, double width, double height, double dpiScaleX, double dpiScaleY, bool isPrimary = false)
    {
        var scaleX = double.IsFinite(dpiScaleX) && dpiScaleX > 0 ? dpiScaleX : 1;
        var scaleY = double.IsFinite(dpiScaleY) && dpiScaleY > 0 ? dpiScaleY : 1;
        return new DisplayBounds(left / scaleX, top / scaleY, width / scaleX, height / scaleY, isPrimary);
    }
}
