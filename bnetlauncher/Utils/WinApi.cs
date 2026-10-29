using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace bnetlauncher.Utils
{
    public static class WinApi
    {
        internal static class NativeMethods
        {
            // Windows Event KeyDown
            public const int WM_KEYDOWN = 0x100;

            // Constant for Enter Key
            public const int VK_RETURN = 0x0D;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;        // x position of upper-left corner
                public int Top;         // y position of upper-left corner
                public int Right;       // x position of lower-right corner
                public int Bottom;      // y position of lower-right corner
            }

#pragma warning disable 649
            public struct INPUT
            {
                public UInt32 Type;
                public MOUSEKEYBDHARDWAREINPUT Data;
            }

            [StructLayout(LayoutKind.Explicit)]
            public struct MOUSEKEYBDHARDWAREINPUT
            {
                [FieldOffset(0)]
                public MOUSEINPUT Mouse;
            }

            public struct MOUSEINPUT
            {
                public Int32 X;
                public Int32 Y;
                public UInt32 MouseData;
                public UInt32 Flags;
                public UInt32 Time;
                public IntPtr ExtraInfo;
            }

            public enum PROCESS_DPI_AWARENESS
            {
                Process_DPI_Unaware = 0,
                Process_System_DPI_Aware = 1,
                Process_Per_Monitor_DPI_Aware = 2
            }

#pragma warning restore 649

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString,int nMaxCount);

            [DllImport("User32.dll")]
            public static extern int SetForegroundWindow(IntPtr point);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("SHCore.dll", SetLastError = true)]
            public static extern bool SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);


            [DllImport("user32.dll")]
            public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            public static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

            [DllImport("user32.dll")]
            public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

            public const int SW_RESTORE = 9;
            public const int SW_SHOWMAXIMIZED = 3;
        }

        #region Unusued functions
        //static public bool IsForegroundWindowByTitle(string title)
        //{
        //    IntPtr hwnd = NativeMethods.GetForegroundWindow();

        //    int length = NativeMethods.GetWindowTextLength(hwnd);
        //    StringBuilder windowtitle = new StringBuilder(length + 1);
        //    int result_len = NativeMethods.GetWindowText(hwnd, windowtitle, windowtitle.Capacity);

        //    if (result_len != length)
        //    {
        //        Logger.Warning($"Got missmatched Windows title length. {length} vs {result_len}");
        //        return false;
        //    }

        //    Logger.Information($"Foreground Window title = '{windowtitle.ToString()}'");
        //    return (windowtitle.ToString().Equals(title, StringComparison.OrdinalIgnoreCase));
        //}

        //static public bool SetForegroundWindowByTitle(string title)
        //{
        //    try
        //    {
        //        var client = Process.GetProcessesByName(title)[0];
        //        return NativeMethods.SetForegroundWindow(client.MainWindowHandle) != 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Exception wile trying to bring '{title}' to the foreground.", ex);
        //    }

        //    return false;
        //}

        //static public bool SetForegroundWindowByHandle(IntPtr handle)
        //{
        //    if (handle != null)
        //    {
        //        return NativeMethods.SetForegroundWindow(handle) != 0;
        //    }
        //    return false;
        //}

        //static public bool SendEnterByTitle(string title)
        //{
        //    var windows = Process.GetProcesses();

        //    bool sent = false;
        //    foreach (var window in windows)
        //    {
        //        if (window.MainWindowTitle == title)
        //        {
        //            NativeMethods.SendMessage(window.MainWindowHandle,
        //                NativeMethods.WM_KEYDOWN, NativeMethods.VK_RETURN, IntPtr.Zero);

        //            Logger.Information($"Sending Enter to '{window.MainWindowTitle}'");
        //            sent = true;
        //        }
        //    }

        //    return sent;
        //}
        #endregion unused functions

        static public void SendEnterByHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Logger.Error($"Given null handle. aborting...");
                return;
            }

            Logger.Information("Sending enter key to window");
            NativeMethods.SendMessage(handle, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_RETURN, IntPtr.Zero);
        }

        // Function based of publicly published code
        // https://stackoverflow.com/questions/30965343/printwindow-could-not-print-google-chrome-window-chrome-widgetwin-1
        public static Bitmap CaptureProcessMainWindow(Process proc)
        {
            if (null == proc)
            {
                Logger.Error($"{nameof(proc)} is null.");
                throw new ArgumentNullException(nameof(proc));
            }

            // Doesn't seem to be needed
            //NativeMethods.SetProcessDpiAwareness(NativeMethods.PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);

            NativeMethods.RECT wnd;
            NativeMethods.GetWindowRect(new HandleRef(proc, proc.MainWindowHandle), out wnd);
            Logger.Information($"Found window at position: {wnd.Top},{wnd.Left}.");

            int width = wnd.Right - wnd.Left;
            int height = wnd.Bottom - wnd.Top;
            if (width <= 0 || height <= 0)
            {
                Logger.Warning($"Window has invalid size {width}x{height}, skipping capture.");
                return new Bitmap(1, 1);
            }
            var bmp = new Bitmap(width, height);  // content only

            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                IntPtr hDC = graphics.GetHdc();
                try { NativeMethods.PrintWindow(proc.MainWindowHandle, hDC, (uint)0x00000002); }
                finally { graphics.ReleaseHdc(hDC); }
            }
            return bmp;
        }

        public static Point FindColorInProcessMainWindow(Process proc, Color color)
        {
            var bmp = CaptureProcessMainWindow(proc);
            bmp.Save(Path.Combine(Program.DataPath, $"{proc.ProcessName}_window_capture.bmp"));

            // Search only the bottom 200px and left 400px - that's where the Play button lives.
            // Scanning the full window picks up banners and icons that share the same blue color.
            int searchLeft = Math.Min(400, bmp.Width);
            int searchTop = Math.Max(0, bmp.Height - 200);

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            bool found = false;

            for (int y = bmp.Height - 1; y >= searchTop; y--)
            {
                for (int x = 0; x < searchLeft; x++)
                {
                    var pixel = bmp.GetPixel(x, y);
                    if (pixel == color)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        found = true;
                    }
                }
            }

            bmp.Dispose();

            if (found)
            {
                var center = new Point((minX + maxX) / 2, (minY + maxY) / 2);
                Logger.Information($"Found color {color} in Window at {center.X},{center.Y} (bounds: {minX},{minY}-{maxX},{maxY})");
                return center;
            }

            Logger.Warning("Couldn't find color in Window.");
            return Point.Empty;
        }

        /// <summary>
        /// Scans an area of the bitmap for pixels of the given color (within tolerance) and returns
        /// the bounding box and number of matching pixels.
        /// </summary>
        private static bool FindColorBounds(Bitmap bmp, Color color, Rectangle area, int tolerance, out Rectangle bounds, out int count)
        {
            bounds = Rectangle.Empty;
            count = 0;

            area.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
            if (area.Width <= 0 || area.Height <= 0)
            {
                return false;
            }

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            var data = bmp.LockBits(area, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                var buffer = new byte[Math.Abs(stride) * data.Height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        int i = y * stride + x * 4; // BGRA
                        if (Math.Abs(buffer[i + 2] - color.R) <= tolerance &&
                            Math.Abs(buffer[i + 1] - color.G) <= tolerance &&
                            Math.Abs(buffer[i] - color.B) <= tolerance)
                        {
                            count++;
                            int px = x + area.X, py = y + area.Y;
                            if (px < minX) minX = px;
                            if (px > maxX) maxX = px;
                            if (py < minY) minY = py;
                            if (py > maxY) maxY = py;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            if (count == 0)
            {
                return false;
            }

            bounds = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        /// <summary>
        /// Counts the pixels that look like a dimmed version of the play button blue (0,116,224).
        /// When the client shows a popup the page gets darkened by a semi transparent layer, and every
        /// extra popup stacked on top darkens it more (for example the play button goes from
        /// 0,116,224 to 0,46,90 and then to 0,32,62). The ratio between green and blue stays the same,
        /// so that is used instead of looking for one exact color.
        /// </summary>
        private static int CountDimmedPlayButtonPixels(Bitmap bmp, Rectangle area)
        {
            area.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
            if (area.Width <= 0 || area.Height <= 0)
            {
                return 0;
            }

            int count = 0;
            var data = bmp.LockBits(area, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                var buffer = new byte[Math.Abs(stride) * data.Height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        int i = y * stride + x * 4; // BGRA
                        int r = buffer[i + 2], g = buffer[i + 1], b = buffer[i];

                        // Dimmed blue: no red, blue between 40 and 200 (200+ is the normal play button)
                        // and green at about 52% of blue.
                        if (r <= 2 && b >= 40 && b <= 200 && Math.Abs(g - (b * 116 / 224)) <= 3)
                        {
                            count++;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return count;
        }

        /// <summary>
        /// When the battle.net client shows a popup (for example "This folder doesn't contain the
        /// correct version of this game" or the "What's New" window) the rest of the page gets dimmed,
        /// so the play button turns from its normal blue to a much darker one. Some popups don't show
        /// up in the window capture, but the dimmed play button does, so that's used to know a popup is open.
        /// </summary>
        public static bool IsPopupShownInProcessMainWindow(Process proc)
        {
            if (null == proc)
            {
                Logger.Error($"{nameof(proc)} is null.");
                throw new ArgumentNullException(nameof(proc));
            }

            using (var bmp = CaptureProcessMainWindow(proc))
            {
                // Same area where the play button normally lives.
                var playArea = new Rectangle(0, Math.Max(0, bmp.Height - 200), Math.Min(400, bmp.Width), Math.Min(200, bmp.Height));
                return CountDimmedPlayButtonPixels(bmp, playArea) >= 500;
            }
        }

        /// <summary>
        /// Moves the mouse to the given screen position, clicks and puts the mouse back.
        /// </summary>
        private static void ClickAtScreenPoint(Point target)
        {
            var oldPos = Cursor.Position;
            Cursor.Position = target;

            var inputMouseDown = new NativeMethods.INPUT();
            inputMouseDown.Type = 0;
            inputMouseDown.Data.Mouse.Flags = 0x0002;
            var inputMouseUp = new NativeMethods.INPUT();
            inputMouseUp.Type = 0;
            inputMouseUp.Data.Mouse.Flags = 0x0004;
            var inputs = new NativeMethods.INPUT[] { inputMouseDown, inputMouseUp };
            _ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

            Cursor.Position = oldPos;
        }

        /// <summary>
        /// Tries to dismiss a popup shown by the client. Each call tries a different method:
        ///  0 - click the OK button of the "folder doesn't contain the correct version" error, it's always
        ///      centered in the window and can't be seen in the window capture, so it's found by offset.
        ///  1 - click on the empty area to the left of the window, which closes the "What's New" window.
        ///  2 - press Escape, which closes most popups.
        /// Enter is deliberately not used since the "What's New" window has buttons that would start things.
        /// </summary>
        public static void DismissPopupInProcessMainWindow(Process proc, int attempt)
        {
            if (null == proc)
            {
                Logger.Error($"{nameof(proc)} is null.");
                throw new ArgumentNullException(nameof(proc));
            }

            NativeMethods.RECT wnd;
            NativeMethods.GetWindowRect(new HandleRef(proc, proc.MainWindowHandle), out wnd);

            switch (attempt % 3)
            {
                case 0:
                    {
                        // OK button position relative to the window center (measured on a 2560x1440 screen).
                        var target = new Point((wnd.Left + wnd.Right) / 2 - 219, (wnd.Top + wnd.Bottom) / 2 - 88);
                        Logger.Information($"Clicking popup OK button at screen position {target.X},{target.Y}");
                        ClickAtScreenPoint(target);
                        break;
                    }
                case 1:
                    {
                        // Empty area on the left side of the window, outside of any popup.
                        int width = wnd.Right - wnd.Left;
                        int height = wnd.Bottom - wnd.Top;
                        var target = new Point(wnd.Left + width * 12 / 100, wnd.Top + height * 55 / 100);
                        Logger.Information($"Clicking outside of popup at screen position {target.X},{target.Y}");
                        ClickAtScreenPoint(target);
                        break;
                    }
                default:
                    Logger.Information("Sending Escape key to dismiss popup");
                    NativeMethods.keybd_event(0x1B, 0, 0, UIntPtr.Zero);
                    NativeMethods.keybd_event(0x1B, 0, 0x0002, UIntPtr.Zero);
                    break;
            }
        }

        /// <summary>
        /// Gets the current size of the process main window, or an empty size if it has no window.
        /// </summary>
        public static Size GetProcessMainWindowSize(Process proc)
        {
            if (null == proc)
            {
                Logger.Error($"{nameof(proc)} is null.");
                throw new ArgumentNullException(nameof(proc));
            }

            if (proc.MainWindowHandle == IntPtr.Zero)
            {
                return Size.Empty;
            }

            NativeMethods.RECT wnd;
            if (!NativeMethods.GetWindowRect(new HandleRef(proc, proc.MainWindowHandle), out wnd))
            {
                return Size.Empty;
            }

            return new Size(wnd.Right - wnd.Left, wnd.Bottom - wnd.Top);
        }

        // Function based of publicly published code
        // reference: https://stackoverflow.com/questions/50380955/c-how-to-get-the-color-of-specific-area-inside-an-image/50387901
        public static Color GetDominantColor(this Bitmap bitmap, Rectangle area)
        {
            if (null == bitmap)
            {
                Logger.Error("null bitmap");
                return Color.Black;
            }

            // Make sure to stay in bounds
            int areaWidth = Math.Min(bitmap.Width, (area.X + area.Width));
            int areaHeight = Math.Min(bitmap.Height, (area.Y + area.Height));

            //Used for tally
            int r = 0;
            int g = 0;
            int b = 0;
            int totalPixels = 0;

            for (int x = area.X; x < areaWidth; x++)
            {
                for (int y = area.Y; y < areaHeight; y++)
                {
                    Color c = bitmap.GetPixel(x, y);

                    r += Convert.ToInt32(c.R);
                    g += Convert.ToInt32(c.G);
                    b += Convert.ToInt32(c.B);

                    totalPixels++;
                }
            }

            r /= totalPixels;
            g /= totalPixels;
            b /= totalPixels;

            return Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
        }

        // Function based of publicly published code
        // https://stackoverflow.com/questions/10355286/programmatically-mouse-click-in-another-window
        public static void ClickWithinWindow(IntPtr wndHandle, Point clientPoint)
        {
            var oldPos = Cursor.Position;

            /// get screen coordinates
            NativeMethods.ClientToScreen(wndHandle, ref clientPoint);

            /// set cursor on coords, and press mouse
            Cursor.Position = new Point(clientPoint.X, clientPoint.Y);

            var inputMouseDown = new NativeMethods.INPUT();
            inputMouseDown.Type = 0; /// input type mouse
            inputMouseDown.Data.Mouse.Flags = 0x0002; /// left button down

            var inputMouseUp = new NativeMethods.INPUT();
            inputMouseUp.Type = 0; /// input type mouse
            inputMouseUp.Data.Mouse.Flags = 0x0004; /// left button up

            var inputs = new NativeMethods.INPUT[] { inputMouseDown, inputMouseUp };
            _ = NativeMethods.SetForegroundWindow(wndHandle);
            _ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

            /// return mouse
            Cursor.Position = oldPos;
        }
    }
}
