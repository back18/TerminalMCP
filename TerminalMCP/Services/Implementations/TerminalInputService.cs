using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using TerminalMCP.Interop;
using TerminalMCP.Models;

namespace TerminalMCP.Services.Implementations
{
    public class TerminalInputService : ITerminalInputService
    {
        public TerminalInputService(
            IClipboardService clipboardService,
            IClipboardLockService clipboardLockService,
            ILogger<TerminalInputService> logger)
        {
            ArgumentNullException.ThrowIfNull(clipboardService, nameof(clipboardService));
            ArgumentNullException.ThrowIfNull(clipboardLockService, nameof(clipboardLockService));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _clipboardService = clipboardService;
            _clipboardLockService = clipboardLockService;
            _logger = logger;
        }

        private readonly IClipboardService _clipboardService;
        private readonly IClipboardLockService _clipboardLockService;
        private readonly ILogger<TerminalInputService> _logger;

        public InputResult TypeText(nint hwnd, string text, bool pressEnter)
        {
            ArgumentException.ThrowIfNullOrEmpty(text, nameof(text));

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("TypeText: hwnd=0x{hwnd:X} textLen={len} pressEnter={enter}", hwnd, text.Length, pressEnter);

            string? clipboardBackup = null;

            _clipboardLockService.Wait();
            try
            {
                _clipboardService.TryReadText(out clipboardBackup);
                if (!_clipboardService.TrySetText(text))
                {
                    _logger.LogWarning("TypeText: failed to set clipboard for hwnd=0x{hwnd:X}", hwnd);
                    return new InputResult(false);
                }

                NativeMethods.FocusWindow(hwnd);
                Thread.Sleep(100);

                // Ctrl+V
                NativeMethods.SendKeyCombo(NativeMethods.VkCtrl, NativeMethods.VkV);
                Thread.Sleep(100);

                if (pressEnter)
                {
                    NativeMethods.SendKey(NativeMethods.VkReturn);
                    Thread.Sleep(30);
                }

                return new InputResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TypeText: failed for hwnd=0x{hwnd:X}", hwnd);
                return new InputResult(false);
            }
            finally
            {
                // Best-effort clipboard restore
                try
                {
                    if (!string.IsNullOrEmpty(clipboardBackup))
                        _clipboardService.TrySetText(clipboardBackup);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TypeText: failed to restore clipboard for hwnd=0x{hwnd:X}", hwnd);
                }

                _clipboardLockService.Release();
            }
        }

        public KeyResult SendKey(nint hwnd, string key)
        {
            ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("SendKey: hwnd=0x{hwnd:X} key='{key}'", hwnd, key);

            try
            {
                int? vk = KeyNameToVk(key);
                if (vk is null)
                {
                    _logger.LogWarning("SendKey: unknown key '{key}'", key);
                    return new KeyResult(false, key);
                }

                NativeMethods.FocusWindow(hwnd);
                Thread.Sleep(50);
                NativeMethods.SendKey(vk.Value);

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("SendKey: sent key '{key}' (VK=0x{vk:X}) to hwnd=0x{hwnd:X}", key, vk.Value, hwnd);
                return new KeyResult(true, key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendKey: failed for hwnd=0x{hwnd:X} key='{key}'", hwnd, key);
                return new KeyResult(false, key);
            }
        }

        private static int? KeyNameToVk(string key)
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
