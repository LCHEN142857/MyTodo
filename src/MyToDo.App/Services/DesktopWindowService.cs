using System.Runtime.InteropServices;
using MyToDo.App.Settings;

namespace MyToDo.App.Services;

public sealed class DesktopWindowService : IDesktopWindowService
{
    private const uint ProgmanSpawnWorkerMessage = 0x052C;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotTopmost = new(-2);

    public IReadOnlyList<DisplayBounds> GetDisplayBounds() => System.Windows.Forms.Screen.AllScreens
        .Select(screen =>
        {
            var area = screen.WorkingArea;
            var (scaleX, scaleY) = GetSystemScale();
            return DisplayBounds.FromPixels(area.Left, area.Top, area.Width, area.Height, scaleX, scaleY, screen.Primary);
        })
        .ToArray();

    public void DetachFromDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;
        SetParent(windowHandle, IntPtr.Zero);
        SetWindowPos(windowHandle, HwndNotTopmost, 0, 0, 0, 0, SetWindowPosNoMove | SetWindowPosNoSize | SetWindowPosNoActivate | SetWindowPosShowWindow);
    }

    public AppSettings EnsureVisible(AppSettings settings, IReadOnlyList<DisplayBounds> displays) => WindowBounds.EnsureVisible(settings, displays);

    public void AttachToDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;

        var worker = FindDesktopWorker();
        if (worker == IntPtr.Zero)
        {
            SetParent(windowHandle, IntPtr.Zero);
            SetWindowPos(windowHandle, HwndNotTopmost, 0, 0, 0, 0, SetWindowPosNoMove | SetWindowPosNoSize | SetWindowPosNoActivate | SetWindowPosShowWindow);
            return;
        }

        SetParent(windowHandle, worker);
        SetWindowPos(windowHandle, HwndNotTopmost, 0, 0, 0, 0, SetWindowPosNoMove | SetWindowPosNoSize | SetWindowPosNoActivate | SetWindowPosShowWindow);
    }

    public void SetTopmost(IntPtr windowHandle, bool isTopmost)
    {
        if (windowHandle == IntPtr.Zero) return;
        if (isTopmost) SetParent(windowHandle, IntPtr.Zero);
        SetWindowPos(windowHandle, isTopmost ? HwndTopmost : HwndNotTopmost, 0, 0, 0, 0, SetWindowPosNoMove | SetWindowPosNoSize | SetWindowPosNoActivate | SetWindowPosShowWindow);
    }

    private static IntPtr FindDesktopWorker()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            SendMessageTimeout(progman, ProgmanSpawnWorkerMessage, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        }

        IntPtr worker = IntPtr.Zero;
        EnumWindows((topLevel, _) =>
        {
            var defView = FindWindowEx(topLevel, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero) return true;
            worker = FindWindowEx(IntPtr.Zero, topLevel, "WorkerW", null);
            return worker == IntPtr.Zero;
        }, IntPtr.Zero);
        return worker;
    }

    public void ActivateWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;
        ShowWindow(windowHandle, ShowWindowRestore);
        SetForegroundWindow(windowHandle);
    }

    private static (double X, double Y) GetSystemScale()
    {
        try
        {
            var dpi = GetDpiForSystem();
            if (dpi > 0) return (dpi / 96d, dpi / 96d);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }

        return (1, 1);
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    private const int ShowWindowRestore = 9;

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindow(string className, string? windowName);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}

public static class WindowBounds
{
    public static AppSettings EnsureVisible(AppSettings settings, IReadOnlyList<DisplayBounds> displays)
    {
        var normalized = settings.Normalize();
        if (displays.Count == 0) return normalized;

        if (displays.Any(display => Intersects(normalized, display))) return normalized;

        var primary = displays.FirstOrDefault(display => display.IsPrimary);
        if (primary.Width <= 0 || primary.Height <= 0) primary = displays[0];
        var maxLeft = Math.Max(primary.Left, primary.Right - normalized.Width);
        var maxTop = Math.Max(primary.Top, primary.Bottom - normalized.Height);
        return normalized with
        {
            Left = Math.Clamp(normalized.Left, primary.Left, maxLeft),
            Top = Math.Clamp(normalized.Top, primary.Top, maxTop)
        };
    }

    private static bool Intersects(AppSettings settings, DisplayBounds display) =>
        settings.Left < display.Right && settings.Left + settings.Width > display.Left &&
        settings.Top < display.Bottom && settings.Top + settings.Height > display.Top;
}

public static class WindowActivation
{
    public static void Activate(bool isTopmost, Action<bool> setTopmost, Action activate, Action? restoreDesktopPlacement = null)
    {
        ArgumentNullException.ThrowIfNull(setTopmost);
        ArgumentNullException.ThrowIfNull(activate);
        if (isTopmost)
        {
            activate();
            return;
        }

        setTopmost(true);
        try
        {
            activate();
        }
        finally
        {
            setTopmost(false);
            restoreDesktopPlacement?.Invoke();
        }
    }
}
