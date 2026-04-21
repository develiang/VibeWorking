using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InputStats;

public sealed class InputHook : IDisposable
{
    private nint _mouseHook;
    private nint _keyboardHook;
    private readonly NativeMethods.LowLevelProc _mouseProc;
    private readonly NativeMethods.LowLevelProc _kbProc;

    public event Action<int, int>? MouseMoved;
    public event Action<int, int>? MouseClicked;
    public event Action<int>? KeyPressed;

    public InputHook()
    {
        _mouseProc = MouseCallback;
        _kbProc = KeyboardCallback;
        NativeMethods.SetProcessDPIAware();
    }

    public void Start()
    {
        using var proc = Process.GetCurrentProcess();
        using var module = proc.MainModule;
        var hMod = NativeMethods.GetModuleHandle(module?.ModuleName);

        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, hMod, 0);
        if (_mouseHook == 0)
        {
            int err = Marshal.GetLastWin32Error();
            Logger.Error($"安装鼠标钩子失败，Win32 错误码: {err}");
        }
        else
        {
            Logger.Info("鼠标钩子已安装");
        }

        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbProc, hMod, 0);
        if (_keyboardHook == 0)
        {
            int err = Marshal.GetLastWin32Error();
            Logger.Error($"安装键盘钩子失败，Win32 错误码: {err}");
        }
        else
        {
            Logger.Info("键盘钩子已安装");
        }
    }

    private nint MouseCallback(int nCode, nint wParam, nint lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                if (wParam == NativeMethods.WM_MOUSEMOVE)
                {
                    var hs = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    MouseMoved?.Invoke(hs.pt.X, hs.pt.Y);
                }
                else if (wParam is NativeMethods.WM_LBUTTONDOWN
                         or NativeMethods.WM_RBUTTONDOWN
                         or NativeMethods.WM_MBUTTONDOWN
                         or NativeMethods.WM_XBUTTONDOWN)
                {
                    var hs = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    MouseClicked?.Invoke(hs.pt.X, hs.pt.Y);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("鼠标回调异常", ex);
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private nint KeyboardCallback(int nCode, nint wParam, nint lParam)
    {
        try
        {
            if (nCode >= 0 && wParam is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                KeyPressed?.Invoke(vkCode);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("键盘回调异常", ex);
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_mouseHook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            Logger.Info("鼠标钩子已卸载");
        }
        if (_keyboardHook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            Logger.Info("键盘钩子已卸载");
        }
    }
}
