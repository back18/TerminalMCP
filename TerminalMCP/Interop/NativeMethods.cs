using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TerminalMCP.Interop
{
    internal static partial class NativeMethods
    {
        public const string WtClassName = "CASCADIA_HOSTING_WINDOW_CLASS";

        public const int VkCtrl = 0x11;
        public const int VkShift = 0x10;
        public const int VkAlt = 0x12;
        public const int VkA = 0x41;
        public const int VkC = 0x43;
        public const int VkV = 0x56;
        public const int VkY = 0x59;
        public const int VkN = 0x4E;
        public const int VkD = 0x44;
        public const int VkReturn = 0x0D;
        public const int VkEscape = 0x1B;
        public const int VkTab = 0x09;
        public const int VkSpace = 0x20;
        public const int VkBack = 0x08;
        public const int VkDelete = 0x2E;
        public const int VkUp = 0x26;
        public const int VkDown = 0x28;
        public const int VkLeft = 0x25;
        public const int VkRight = 0x27;
        public const int VkHome = 0x24;
        public const int VkEnd = 0x23;
        public const int VkF4 = 0x73;
        public const int KeyeventfKeyup = 0x0002;

        public const int WmClose = 0x0010;

        public const int SwRestore = 9;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassNameW(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextW(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsIconic(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        public static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostMessage(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static bool FocusWindow(nint hwnd)
        {
            if (!IsWindow(hwnd))
                return false;

            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SwRestore);
                Thread.Sleep(300);
            }

            bool result = SetForegroundWindow(hwnd);
            Thread.Sleep(200);
            return result;
        }

        public static void SendKey(int vk)
        {
            keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            keybd_event((byte)vk, 0, KeyeventfKeyup, UIntPtr.Zero);
            Thread.Sleep(30);
        }

        public static void SendKeyCombo(params int[] vks)
        {
            if (vks.Length == 0)
                return;

            // Press all keys
            foreach (int vk in vks)
            {
                keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
                Thread.Sleep(30);
            }

            Thread.Sleep(50);

            // Release all keys in reverse order
            for (int i = vks.Length - 1; i >= 0; i--)
            {
                keybd_event((byte)vks[i], 0, KeyeventfKeyup, UIntPtr.Zero);
                Thread.Sleep(30);
            }
        }
    }
}
