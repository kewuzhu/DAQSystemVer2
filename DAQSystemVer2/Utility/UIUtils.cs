using System.Runtime.InteropServices;
using System.Windows;

namespace DAQSystem.Application.Utility
{
    internal class UIUtils
    {
        private static int GWL_STYLE = -16;
        private static int WS_SYSMENU = 0x80000;

        public static bool IsWindowOpen(Window window) =>
             System.Windows.Application.Current.Windows.Cast<Window>().Any(x => x == window);

        public static Window GetActiveWindow() =>
            System.Windows.Application.Current.Windows.Cast<Window>().FirstOrDefault(x => x.IsActive);

        public static void HideWindowTitleBar(IntPtr hwnd) =>
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
