// Tile Windows — double-click desktop tiler for the current virtual desktop.
// Build (Framework 4.x):
//   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:"Tile Windows.exe" /r:System.Windows.Forms.dll TileWindows.cs
// Recovered from IL of the shipped Tile Windows.exe via ildasm (behavior-matched).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class TileWindows
{
    private const int SPI_GETWORKAREA = 0x30;
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_CAPTION = 0x00C00000;
    private const int SW_RESTORE = 9;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int GW_OWNER = 4;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, ref RECT pvParam, int fWinIni);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    private static bool IsTileCandidate(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd)) return false;
        if (IsIconic(hWnd)) return false;
        if (hWnd == GetShellWindow()) return false;
        if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return false;

        var titleBuf = new StringBuilder(0x200);
        if (GetWindowText(hWnd, titleBuf, titleBuf.Capacity) <= 0) return false;
        string title = titleBuf.ToString();
        if (string.IsNullOrWhiteSpace(title)) return false;

        var classBuf = new StringBuilder(0x100);
        GetClassName(hWnd, classBuf, classBuf.Capacity);
        string cls = classBuf.ToString();

        // Skip shell / tray / desktop chrome. Keep ApplicationFrameWindow (UWP hosts).
        if (cls == "Progman"
            || cls == "WorkerW"
            || cls == "Shell_TrayWnd"
            || cls == "Shell_SecondaryTrayWnd"
            || cls == "DV2ControlHost"
            || cls == "Windows.UI.Core.CoreWindow"
            || cls == "ApplicationFrameWindow")
        {
            if (cls != "ApplicationFrameWindow") return false;
        }
        if (cls == "Windows.UI.Core.CoreWindow") return false;

        if (string.Equals(title, "Program Manager", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(title, "Start", StringComparison.OrdinalIgnoreCase)) return false;
        if (title.IndexOf("Tile Windows", StringComparison.OrdinalIgnoreCase) >= 0) return false;

        int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
        if ((ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0) return false;

        int style = GetWindowLong(hWnd, GWL_STYLE);
        if ((style & WS_CAPTION) == 0 && (ex & WS_EX_APPWINDOW) == 0) return false;

        return true;
    }

    [STAThread]
    private static void Main()
    {
        try
        {
            RECT work = new RECT();
            if (!SystemParametersInfo(SPI_GETWORKAREA, 0, ref work, 0))
            {
                MessageBox.Show(
                    "Could not read the desktop work area.",
                    "Tile Windows",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var windows = new List<IntPtr>();
            EnumWindows((hWnd, lParam) =>
            {
                if (IsTileCandidate(hWnd)) windows.Add(hWnd);
                return true;
            }, IntPtr.Zero);

            if (windows.Count == 0)
            {
                MessageBox.Show(
                    "No open windows to tile.",
                    "Tile Windows",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int count = windows.Count;
            int cols, rows;
            if (count == 1)
            {
                cols = 1;
                rows = 1;
            }
            else if (count <= 3)
            {
                cols = count;
                rows = 1;
            }
            else if (count == 4)
            {
                cols = 2;
                rows = 2;
            }
            else
            {
                cols = (int)Math.Ceiling(Math.Sqrt(count));
                rows = (int)Math.Ceiling(count / (double)cols);
            }

            int tileW = work.Width / cols;
            int tileH = work.Height / rows;

            for (int i = 0; i < count; i++)
            {
                IntPtr hWnd = windows[i];
                if (IsZoomed(hWnd) || IsIconic(hWnd))
                    ShowWindow(hWnd, SW_RESTORE);

                int col = i % cols;
                int row = i / cols;
                int remainingInRow = count - row * cols;

                int cellW;
                if (row == rows - 1 && remainingInRow < cols)
                    cellW = work.Width / remainingInRow;
                else
                    cellW = tileW;

                // On a short last row, re-index column within that row.
                if (row == rows - 1 && remainingInRow < cols)
                    col = i - row * cols;

                int x = work.Left + col * cellW;
                int y = work.Top + row * tileH;
                int w = cellW;
                int h = tileH;

                int colsInThisRow = (row == rows - 1 && remainingInRow < cols) ? remainingInRow : cols;
                if (col == colsInThisRow - 1)
                    w = work.Right - x;
                if (row == rows - 1)
                    h = work.Bottom - y;

                SetWindowPos(
                    hWnd,
                    IntPtr.Zero,
                    x, y, w, h,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Tile Windows",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
