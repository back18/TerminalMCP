using System;
using System.Collections.Generic;
using System.Text;
using TerminalMCP.Interop;

namespace TerminalMCP.Utilities
{
    internal static class KeyMapper
    {
        public static int? KeyNameToVk(string key)
        {
            return key.ToLowerInvariant() switch
            {
                "enter" or "return" => NativeMethods.VkReturn,
                "escape" or "esc" => NativeMethods.VkEscape,
                "tab" => NativeMethods.VkTab,
                "space" => NativeMethods.VkSpace,
                "backspace" => NativeMethods.VkBack,
                "delete" or "del" => NativeMethods.VkDelete,
                "up" => NativeMethods.VkUp,
                "down" => NativeMethods.VkDown,
                "left" => NativeMethods.VkLeft,
                "right" => NativeMethods.VkRight,
                "home" => NativeMethods.VkHome,
                "end" => NativeMethods.VkEnd,
                "y" => NativeMethods.VkY,
                "n" => NativeMethods.VkN,
                "a" => NativeMethods.VkA,
                "c" => NativeMethods.VkC,
                "v" => NativeMethods.VkV,
                "d" => NativeMethods.VkD,
                _ => null,
            };
        }
    }
}
