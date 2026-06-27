using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GameTranslator.Platform.Windows.Interop;
using static GameTranslator.Platform.Windows.Interop.NativeMethods;

namespace GameTranslator.Platform.Windows.Capture;

/// <summary>
/// Captures a window's pixels from OUTSIDE the game process using GDI (PrintWindow / BitBlt).
/// This is a pure read — no DLL injection, no DirectX hook, no game-file access — which is
/// what keeps it anti-cheat-friendly (the same mechanism screenshot tools use).
///
/// Works for windowed and borderless-fullscreen games (the common case for online titles).
/// True exclusive-fullscreen may return black; the fix there is Windows Graphics Capture,
/// noted as a future enhancement in docs/BUILD-WINDOWS.md.
/// </summary>
public sealed class WindowCaptureService
{
    /// <summary>Enumerate visible, titled top-level windows for the target picker.</summary>
    public IReadOnlyList<WindowInfo> ListWindows()
    {
        var list = new List<WindowInfo>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            string proc = "";
            try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { }

            list.Add(new WindowInfo(hWnd, title, proc));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public IntPtr ForegroundWindow() => GetForegroundWindow();

    public (string title, string process) DescribeWindow(IntPtr hWnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hWnd, sb, sb.Capacity);
        GetWindowThreadProcessId(hWnd, out var pid);
        string proc = "";
        try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { }
        return (sb.ToString(), proc);
    }

    /// <summary>Client-area size in pixels (cheap; no capture).</summary>
    public (int w, int h) ClientSize(IntPtr hWnd)
    {
        if (!GetClientRect(hWnd, out var rc)) return (0, 0);
        return (rc.Right - rc.Left, rc.Bottom - rc.Top);
    }

    /// <summary>Top-left of the window's client area in screen coordinates (overlay alignment).</summary>
    public (int x, int y) ClientOrigin(IntPtr hWnd)
    {
        var pt = new POINT { X = 0, Y = 0 };
        ClientToScreen(hWnd, ref pt);
        return (pt.X, pt.Y);
    }

    /// <summary>Capture the full client area of a window as BGRA. Returns null on failure.</summary>
    public CapturedFrame? CaptureWindow(IntPtr hWnd)
    {
        if (!GetClientRect(hWnd, out var rc)) return null;
        int width = rc.Right - rc.Left;
        int height = rc.Bottom - rc.Top;
        if (width <= 0 || height <= 0) return null;

        IntPtr srcDc = GetWindowDC(hWnd);
        if (srcDc == IntPtr.Zero) return null;
        IntPtr memDc = CreateCompatibleDC(srcDc);
        IntPtr bmp = CreateCompatibleBitmap(srcDc, width, height);
        IntPtr oldBmp = SelectObject(memDc, bmp);
        try
        {
            // Prefer PrintWindow with full-content rendering (handles DirectX/borderless better).
            bool ok = PrintWindow(hWnd, memDc, PW_RENDERFULLCONTENT);
            if (!ok)
                ok = BitBlt(memDc, 0, 0, width, height, srcDc, 0, 0, SRCCOPY);
            if (!ok) return null;

            return ExtractBgra(memDc, bmp, width, height);
        }
        finally
        {
            SelectObject(memDc, oldBmp);
            DeleteObject(bmp);
            DeleteDC(memDc);
            ReleaseDC(hWnd, srcDc);
        }
    }

    /// <summary>Capture an arbitrary screen rectangle (used by subtitle mode / region profiles).</summary>
    public CapturedFrame? CaptureScreenRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) return null;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) return null;
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr bmp = CreateCompatibleBitmap(screenDc, width, height);
        IntPtr oldBmp = SelectObject(memDc, bmp);
        try
        {
            if (!BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SRCCOPY))
                return null;
            return ExtractBgra(memDc, bmp, width, height);
        }
        finally
        {
            SelectObject(memDc, oldBmp);
            DeleteObject(bmp);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>Pull top-down 32-bit BGRA bytes out of a GDI bitmap via GetDIBits.</summary>
    private static CapturedFrame? ExtractBgra(IntPtr memDc, IntPtr bmp, int width, int height)
    {
        var bmi = new BITMAPINFO
        {
            bmiColors = new uint[256],
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,   // negative => top-down rows
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0     // BI_RGB
            }
        };

        var buffer = new byte[width * height * 4];
        int scanned = GetDIBits(memDc, bmp, 0, (uint)height, buffer, ref bmi, DIB_RGB_COLORS);
        return scanned == 0 ? null : new CapturedFrame(buffer, width, height);
    }
}
