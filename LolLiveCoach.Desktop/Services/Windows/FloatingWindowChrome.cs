using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LolLiveCoach.Desktop.Services;

public static class FloatingWindowChrome
{
    private const int GwlExStyle = -20;
    private const int MonitorDefaultToNearest = 2;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;

    public static void Apply(Window window, bool interactive)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        styles |= WsExToolWindow;

        if (interactive)
        {
            styles &= ~WsExTransparent;
            styles &= ~WsExNoActivate;
        }
        else
        {
            styles |= WsExTransparent;
            styles |= WsExNoActivate;
        }

        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(styles));
    }

    public static Rect GetPlacementArea(Window window)
    {
        var referenceHandle = GetPlacementReferenceHandle(window);
        var monitor = referenceHandle != IntPtr.Zero
            ? MonitorFromWindow(referenceHandle, MonitorDefaultToNearest)
            : IntPtr.Zero;

        return GetPlacementArea(window, monitor);
    }

    public static void ClampToWorkArea(Window window)
    {
        var handle = EnsureHandle(window);
        var monitor = handle != IntPtr.Zero
            ? MonitorFromWindow(handle, MonitorDefaultToNearest)
            : IntPtr.Zero;
        var workArea = GetPlacementArea(window, monitor);
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

        window.Left = Math.Max(workArea.Left, Math.Min(window.Left, workArea.Right - width));
        window.Top = Math.Max(workArea.Top, Math.Min(window.Top, workArea.Bottom - height));
    }

    private static Rect GetPlacementArea(Window window, IntPtr monitor)
    {
        if (monitor == IntPtr.Zero)
        {
            return SystemParameters.WorkArea;
        }

        var monitorInfo = GetMonitorArea(window, monitor);
        return ShouldUseMonitorBounds(monitor)
            ? monitorInfo.Bounds
            : monitorInfo.WorkArea;
    }

    private static IntPtr GetPlacementReferenceHandle(Window window)
    {
        var selfHandle = new WindowInteropHelper(window).Handle;
        var foregroundHandle = GetForegroundWindow();
        if (foregroundHandle != IntPtr.Zero && foregroundHandle != selfHandle)
        {
            return foregroundHandle;
        }

        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow is not null && !ReferenceEquals(mainWindow, window))
        {
            var mainWindowHandle = new WindowInteropHelper(mainWindow).Handle;
            if (mainWindowHandle != IntPtr.Zero)
            {
                return mainWindowHandle;
            }
        }

        return EnsureHandle(window);
    }

    private static IntPtr EnsureHandle(Window window)
    {
        return new WindowInteropHelper(window).EnsureHandle();
    }

    private static bool ShouldUseMonitorBounds(IntPtr monitor)
    {
        var foregroundHandle = GetForegroundWindow();
        if (foregroundHandle == IntPtr.Zero)
        {
            return false;
        }

        var foregroundMonitor = MonitorFromWindow(foregroundHandle, MonitorDefaultToNearest);
        if (foregroundMonitor != monitor || !GetWindowRect(foregroundHandle, out var windowRect))
        {
            return false;
        }

        var monitorInfo = CreateMonitorInfo();
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        const int tolerance = 2;

        return Math.Abs(windowRect.Left - monitorInfo.Monitor.Left) <= tolerance
            && Math.Abs(windowRect.Top - monitorInfo.Monitor.Top) <= tolerance
            && Math.Abs(windowRect.Right - monitorInfo.Monitor.Right) <= tolerance
            && Math.Abs(windowRect.Bottom - monitorInfo.Monitor.Bottom) <= tolerance;
    }

    private static MonitorArea GetMonitorArea(Window window, IntPtr monitor)
    {
        var monitorInfo = CreateMonitorInfo();
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return new MonitorArea(SystemParameters.WorkArea, SystemParameters.WorkArea);
        }

        var dpi = GetMonitorScale(monitor);
        return new MonitorArea(
            ToWpfRect(monitorInfo.Monitor, dpi),
            ToWpfRect(monitorInfo.WorkArea, dpi));
    }

    private static MonitorInfo CreateMonitorInfo()
    {
        return new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
    }

    private static DpiScale GetMonitorScale(IntPtr monitor)
    {
        try
        {
            if (GetDpiForMonitor(monitor, MonitorDpiType.Effective, out var dpiX, out var dpiY) == 0
                && dpiX > 0
                && dpiY > 0)
            {
                return new DpiScale(dpiX / 96d, dpiY / 96d);
            }
        }
        catch
        {
            // Fall back to 1:1 scaling if monitor DPI can't be resolved.
        }

        return new DpiScale(1d, 1d);
    }

    private static Rect ToWpfRect(Rectangle rect, DpiScale dpi)
    {
        var left = rect.Left / dpi.X;
        var top = rect.Top / dpi.Y;
        var width = (rect.Right - rect.Left) / dpi.X;
        var height = (rect.Bottom - rect.Top) / dpi.Y;

        return new Rect(left, top, width, height);
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : GetWindowLongPtr32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, newLong)
            : SetWindowLongPtr32(hWnd, nIndex, newLong);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, int dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rectangle lpRect);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr hmonitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);

    private readonly record struct MonitorArea(Rect Bounds, Rect WorkArea);

    private readonly record struct DpiScale(double X, double Y);

    private enum MonitorDpiType
    {
        Effective = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rectangle Monitor;
        public Rectangle WorkArea;
        public int Flags;
    }
}
