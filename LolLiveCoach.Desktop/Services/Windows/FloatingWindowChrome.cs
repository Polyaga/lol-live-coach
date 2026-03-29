using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LolLiveCoach.Desktop.Services;

public static class FloatingWindowChrome
{
    private const int GwlExStyle = -20;
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

    public static void ClampToWorkArea(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

        window.Left = Math.Max(workArea.Left, Math.Min(window.Left, workArea.Right - width));
        window.Top = Math.Max(workArea.Top, Math.Min(window.Top, workArea.Bottom - height));
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
}
