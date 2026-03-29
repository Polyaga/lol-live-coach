using System.Runtime.InteropServices;

namespace LolLiveCoach.Desktop.Services;

public sealed class GlobalTabKeyMonitor : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkTab = 0x09;

    private readonly LowLevelKeyboardProc _hookCallback;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _tabIsDown;

    public GlobalTabKeyMonitor()
    {
        _hookCallback = HookCallback;
    }

    public event Action<bool>? TabStateChanged;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookCallback, IntPtr.Zero, 0);
    }

    public void Dispose()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var keyInfo = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
            if (keyInfo.VkCode == VkTab)
            {
                var message = wParam.ToInt32();
                if ((message == WmKeyDown || message == WmSysKeyDown) && !_tabIsDown)
                {
                    _tabIsDown = true;
                    TabStateChanged?.Invoke(true);
                }
                else if ((message == WmKeyUp || message == WmSysKeyUp) && _tabIsDown)
                {
                    _tabIsDown = false;
                    TabStateChanged?.Invoke(false);
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Kbdllhookstruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
