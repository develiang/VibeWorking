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
    public event Action? MouseClicked;
    public event Action? KeyPressed;

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
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbProc, hMod, 0);
    }

    private nint MouseCallback(int nCode, nint wParam, nint lParam)
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
                MouseClicked?.Invoke();
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private nint KeyboardCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
        {
            KeyPressed?.Invoke();
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_mouseHook != 0)
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != 0)
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
    }
}
