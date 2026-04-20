using System.Runtime.InteropServices;

namespace InputStats;

public static class DistanceCalculator
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativeMethods.POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>
    /// 计算两点间像素距离对应的物理长度（厘米），按目标点所在显示器 DPI 补偿。
    /// </summary>
    public static double PixelsToCm(int x1, int y1, int x2, int y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double px = Math.Sqrt(dx * dx + dy * dy);
        if (px == 0)
            return 0;

        var pt = new NativeMethods.POINT { X = x2, Y = y2 };
        var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

        uint dpi = 96;
        if (hMon != 0)
        {
            var hr = GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out dpi, out _);
            if (hr != 0)
                dpi = 96;
        }

        return px / dpi * 2.54;
    }
}
