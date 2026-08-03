using System;
using UnityEngine;

namespace Mate.Platform.Windows
{
    internal static class WindowsDesktopWindowUtility
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;

        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const long WsMinimizeBox = 0x00020000L;
        private const long WsMaximizeBox = 0x00010000L;
        private const long WsSysMenu = 0x00080000L;

        private const long WsExLayered = 0x00080000L;
        private const long WsExTransparent = 0x00000020L;
        private const long WsExToolWindow = 0x00000080L;
        private const long WsExAppWindow = 0x00040000L;

        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;

        private const uint LwaColorKey = 0x00000001;
        private const uint LwaAlpha = 0x00000002;
        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;
        private const int VkLButton = 0x01;

        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        public static bool IsSupported => true;

        public static bool TryFindUnityWindow(out IntPtr hwnd)
        {
            hwnd = GetActiveWindow();
            if (hwnd != IntPtr.Zero)
            {
                return true;
            }

            var processId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            var found = IntPtr.Zero;
            EnumWindows((candidate, _) =>
            {
                GetWindowThreadProcessId(candidate, out var ownerProcessId);
                if (ownerProcessId == processId && IsWindowVisible(candidate) && GetWindow(candidate, 4) == IntPtr.Zero)
                {
                    found = candidate;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            hwnd = found;
            return hwnd != IntPtr.Zero;
        }

        public static bool TryGetPrimaryDisplayBounds(out RectInt bounds)
        {
            var width = GetSystemMetrics(SmCxScreen);
            var height = GetSystemMetrics(SmCyScreen);
            bounds = new RectInt(0, 0, width, height);
            return width > 0 && height > 0;
        }

        public static void ApplyBorderless(IntPtr hwnd, bool hideFromAltTab)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var style = GetWindowLongPtr64(hwnd, GwlStyle).ToInt64();
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            SetWindowLongPtr64(hwnd, GwlStyle, new IntPtr(style));

            var exStyle = GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64();
            exStyle |= WsExLayered;
            if (hideFromAltTab)
            {
                exStyle |= WsExToolWindow;
                exStyle &= ~WsExAppWindow;
            }

            SetWindowLongPtr64(hwnd, GwlExStyle, new IntPtr(exStyle));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        public static void ApplyTransparentFrame(IntPtr hwnd, Color32 colorKey)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var exStyle = GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64() | WsExLayered;
            SetWindowLongPtr64(hwnd, GwlExStyle, new IntPtr(exStyle));
            if (!SetLayeredWindowAttributes(hwnd, ToColorRef(colorKey), 0, LwaColorKey))
            {
                Debug.LogWarning($"SetLayeredWindowAttributes failed: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
            }

            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        private static uint ToColorRef(Color32 color)
        {
            return (uint)(color.r | (color.g << 8) | (color.b << 16));
        }

        public static void SetWindowBounds(IntPtr hwnd, RectInt bounds, bool topMost)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                hwnd,
                topMost ? HwndTopMost : HwndNoTopMost,
                bounds.x,
                bounds.y,
                bounds.width,
                bounds.height,
                SwpShowWindow | SwpFrameChanged);
        }

        public static void SetTopMost(IntPtr hwnd, bool topMost)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                hwnd,
                topMost ? HwndTopMost : HwndNoTopMost,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }

        public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var exStyle = GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64() | WsExLayered;
            var nextStyle = clickThrough ? exStyle | WsExTransparent : exStyle & ~WsExTransparent;
            if (nextStyle == exStyle)
            {
                return;
            }

            SetWindowLongPtr64(hwnd, GwlExStyle, new IntPtr(nextStyle));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        public static bool TryGetClientCursorPosition(IntPtr hwnd, out Vector2 bottomLeftClientPosition)
        {
            bottomLeftClientPosition = default;
            if (hwnd == IntPtr.Zero || !GetCursorPos(out var point) || !GetClientRect(hwnd, out var rect))
            {
                return false;
            }

            if (!ScreenToClient(hwnd, ref point))
            {
                return false;
            }

            bottomLeftClientPosition = new Vector2(point.X, rect.Bottom - point.Y);
            return true;
        }

        public static bool IsLeftMouseButtonDown()
        {
            return (GetAsyncKeyState(VkLButton) & 0x8000) != 0;
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr newLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hwnd, ref Point point);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
#else
        public static bool IsSupported => false;

        public static bool TryFindUnityWindow(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            return false;
        }

        public static bool TryGetPrimaryDisplayBounds(out RectInt bounds)
        {
            bounds = default;
            return false;
        }

        public static void ApplyBorderless(IntPtr hwnd, bool hideFromAltTab)
        {
        }

        public static void ApplyTransparentFrame(IntPtr hwnd, Color32 colorKey)
        {
        }

        public static void SetWindowBounds(IntPtr hwnd, RectInt bounds, bool topMost)
        {
        }

        public static void SetTopMost(IntPtr hwnd, bool topMost)
        {
        }

        public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
        {
        }

        public static bool TryGetClientCursorPosition(IntPtr hwnd, out Vector2 bottomLeftClientPosition)
        {
            bottomLeftClientPosition = default;
            return false;
        }

        public static bool IsLeftMouseButtonDown()
        {
            return false;
        }
#endif
    }
}
