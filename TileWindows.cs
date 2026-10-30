// Tile Windows - double-click desktop tiler for the current virtual desktop.
// Build (Framework 4.x):
//   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:"Tile Windows.exe" TileWindows.cs
//
// v2 layout fixes:
//   - Per-monitor DPI aware, so tile sizes are real pixels (no shrunken windows on scaled displays).
//   - Compensates for the invisible DWM resize border, so visible frames sit flush with no gaps.
//   - Column count follows the screen's aspect ratio (ultrawides get more columns), and every
//     row is split across the full width, so there are no empty cells.
//   - Skips cloaked windows (other virtual desktops), no-activate windows, and fixed-size dialogs
//     that can't be resized to fill a tile.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

static class TileWindows
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    delegate bool EnumWindowsProc(IntPtr h, IntPtr l);

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint p, ref RECT r, uint w);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBox(IntPtr h, string t, string c, uint type);

    const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    const int WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000;
    const int WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000;
    const uint GW_OWNER = 4, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_FRAMECHANGED = 0x20;
    const int SW_RESTORE = 9, DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14;
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);

    static readonly HashSet<string> SkipClasses = new HashSet<string> {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "DV2ControlHost", "Windows.UI.Core.CoreWindow" };

    static bool IsTileCandidate(IntPtr h)
    {
        if (h == GetShellWindow() || !IsWindowVisible(h) || IsIconic(h)) return false;
        int cloaked; if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return false;
        var cls = new StringBuilder(256); GetClassName(h, cls, 256);
        if (SkipClasses.Contains(cls.ToString())) return false;
        var title = new StringBuilder(512); GetWindowText(h, title, 512);
        string t = title.ToString();
        if (t.Length == 0 || t == "Program Manager" || t == "Start" || t == "Tile Windows") return false;
        int style = GetWindowLong(h, GWL_STYLE), ex = GetWindowLong(h, GWL_EXSTYLE);
        if ((ex & WS_EX_TOOLWINDOW) != 0 || (ex & WS_EX_NOACTIVATE) != 0) return false;
        if (GetWindow(h, GW_OWNER) != IntPtr.Zero && (ex & WS_EX_APPWINDOW) == 0) return false;
        if ((style & WS_CAPTION) != WS_CAPTION) return false;
        // Fixed-size dialogs (no resize border) can't fill a tile, so leave them where they are.
        if ((style & WS_THICKFRAME) == 0) return false;
        RECT r; GetWindowRect(h, out r);
        if (r.Right - r.Left < 100 || r.Bottom - r.Top < 60) return false;
        return true;
    }

    static void Place(IntPtr h, int x, int y, int w, int hgt)
    {
        if (IsZoomed(h) || IsIconic(h)) ShowWindow(h, SW_RESTORE);
        SetWindowPos(h, IntPtr.Zero, x, y, w, hgt, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        // Windows 10/11 draw an invisible resize border; grow the window so the visible frame fills the tile.
        RECT outer, vis;
        if (GetWindowRect(h, out outer) && DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out vis, Marshal.SizeOf(typeof(RECT))) == 0)
        {
            int l = vis.Left - outer.Left, t = vis.Top - outer.Top, rr = outer.Right - vis.Right, b = outer.Bottom - vis.Bottom;
            if (l != 0 || t != 0 || rr != 0 || b != 0)
                SetWindowPos(h, IntPtr.Zero, x - l, y - t, w + l + rr, hgt + t + b, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    [STAThread]
    static void Main()
    {
        // Per-monitor DPI aware so sizes are real pixels (otherwise Windows scales them down and tiles come out small).
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); } catch { try { SetProcessDPIAware(); } catch { } }

        RECT work = new RECT();
        if (!SystemParametersInfo(0x0030, 0, ref work, 0)) { MessageBox(IntPtr.Zero, "Could not read the desktop work area.", "Tile Windows", 0x10); return; }

        var wins = new List<IntPtr>();
        EnumWindows((h, l) => { if (IsTileCandidate(h)) wins.Add(h); return true; }, IntPtr.Zero);
        int n = wins.Count;
        if (n == 0) { MessageBox(IntPtr.Zero, "No open windows to tile.", "Tile Windows", 0x40); return; }

        int W = work.Right - work.Left, H = work.Bottom - work.Top;
        // Pick a column count that keeps tiles roughly 16:10 on this screen shape (ultrawide gets more columns).
        int cols = (int)Math.Ceiling(Math.Sqrt(n * (double)W / H / 1.6));
        if (cols < 1) cols = 1; if (cols > n) cols = n;
        int rows = (int)Math.Ceiling(n / (double)cols);
        // Re-balance so rows are as even as possible.
        cols = (int)Math.Ceiling(n / (double)rows);

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            int remaining = n - idx;
            int inRow = Math.Min(cols, (int)Math.Ceiling(remaining / (double)(rows - r)));
            int y0 = work.Top + (int)((long)H * r / rows), y1 = work.Top + (int)((long)H * (r + 1) / rows);
            for (int c = 0; c < inRow; c++)
            {
                // Every row is split across the full width, so there are no empty cells.
                int x0 = work.Left + (int)((long)W * c / inRow), x1 = work.Left + (int)((long)W * (c + 1) / inRow);
                Place(wins[idx++], x0, y0, x1 - x0, y1 - y0);
            }
        }
    }
}
