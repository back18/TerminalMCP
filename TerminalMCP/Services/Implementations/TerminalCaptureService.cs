using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using TerminalMCP.Interop;
using TerminalMCP.Models;
using TerminalMCP.Utilities;

namespace TerminalMCP.Services.Implementations
{
    public class TerminalCaptureService : ITerminalCaptureService
    {
        public TerminalCaptureService(IClipboardService clipboardService, IClipboardLockService clipboardLockService, ILogger<TerminalCaptureService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(clipboardService, nameof(clipboardService));
            ArgumentNullException.ThrowIfNull(clipboardLockService, nameof(clipboardLockService));

            _logger = logger;
            _clipboardService = clipboardService;
            _clipboardLockService = clipboardLockService;
        }

        private const int DiffCooldownMs = 5000;

        private readonly ILogger<TerminalCaptureService> _logger;
        private readonly IClipboardService _clipboardService;
        private readonly IClipboardLockService _clipboardLockService;
        private readonly ConcurrentDictionary<nint, string[]> _baselines = new();
        private readonly ConcurrentDictionary<nint, TerminalInfo> _windowCache = new();
        private readonly ConcurrentDictionary<nint, DateTime> _diffCooldowns = new();
        private bool _disposed;

        public IReadOnlyList<TerminalInfo> EnumerateWindows()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            List<TerminalInfo> results = [];
            HashSet<nint> seen = [];
            int totalWindows = 0;
            int visibleWindows = 0;
            int nonWtWindows = 0;

            NativeMethods.EnumWindows((hWnd, _) =>
            {
                nint hwnd = hWnd;
                totalWindows++;

                if (seen.Contains(hwnd))
                    return true;

                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                visibleWindows++;

                string className = GetWindowClassName(hwnd);

                if (className != NativeMethods.WtClassName)
                {
                    nonWtWindows++;
                    return true;
                }

                seen.Add(hwnd);

                string title = GetWindowTitle(hwnd);

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("EnumerateWindows: found WT window hwnd=0x{hwnd:X} title='{title}'", hwnd, title);

                results.Add(new TerminalInfo(hwnd.ToInt32(), title, 0, string.Empty));
                return true;
            }, IntPtr.Zero);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("EnumerateWindows: done - total={total} visible={visible} nonWt={nonWt} found={found}",
                    totalWindows, visibleWindows, nonWtWindows, results.Count);

            return results;
        }

        public IReadOnlyList<TerminalInfo> Init()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            List<TerminalInfo> results = [];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Init: starting, cached windows={count}", _windowCache.Count);

            // Get current WT windows
            IReadOnlyList<TerminalInfo> currentWindows = EnumerateWindows();
            HashSet<nint> currentHwnds = [.. currentWindows.Select(s => s.Hwnd)];

            // Remove stale entries
            foreach (nint hwnd in _windowCache.Keys)
            {
                if (!currentHwnds.Contains(hwnd))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Init: removing stale window hwnd=0x{hwnd:X}", hwnd);
                    _windowCache.TryRemove(hwnd, out _);
                    _baselines.TryRemove(hwnd, out _);
                }
            }

            // Discover new windows (incremental)
            foreach (TerminalInfo info in currentWindows)
            {
                if (_windowCache.ContainsKey(info.Hwnd))
                {
                    // Update title only (window may have changed tabs)
                    string title = GetWindowTitle(info.Hwnd);

                    TerminalInfo cached = _windowCache[info.Hwnd];
                    TerminalInfo updated = cached with { Title = title };
                    _windowCache[info.Hwnd] = updated;
                    results.Add(updated);
                }
                else
                {
                    if (!IsValidTerminalWindow(info.Hwnd))
                        continue;

                    // New window: capture content
                    NativeMethods.FocusWindow(info.Hwnd);
                    string? text = CaptureContent() ?? string.Empty;
                    string[] lines = TextHelper.SplitLines(text);
                    string tailPreview = string.Join("\n", lines[^Math.Min(20, lines.Length)..]);

                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Init: captured hwnd=0x{hwnd:X} lines={lineCount}", info.Hwnd, lines.Length);

                    TerminalInfo captured = new(info.Hwnd, info.Title, lines.Length, tailPreview);
                    _windowCache[info.Hwnd] = captured;
                    _baselines[info.Hwnd] = lines;
                    results.Add(captured);
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Init: done - returned {count} windows", results.Count);

            return results;
        }

        public TerminalInfo? Init(nint hwnd)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsValidTerminalWindow(hwnd))
            {
                _logger.LogWarning("InitWindow: invalid window hwnd=0x{hwnd:X}", hwnd);
                return null;
            }

            string title = GetWindowTitle(hwnd);

            NativeMethods.FocusWindow(hwnd);
            string? text = CaptureContent() ?? string.Empty;
            string[] lines = TextHelper.SplitLines(text);
            string tailPreview = string.Join("\n", lines[^Math.Min(20, lines.Length)..]);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Init: captured hwnd=0x{hwnd:X} lines={lineCount}", hwnd, lines.Length);

            TerminalInfo captured = new(hwnd.ToInt32(), title, lines.Length, tailPreview);
            _windowCache[hwnd] = captured;
            _baselines[hwnd] = lines;

            return captured;
        }

        public ReadResult ReadContent(nint hwnd, int offset, int limit)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("ReadContent: hwnd=0x{hwnd:X} offset={offset} limit={limit}", hwnd, offset, limit);

            if (!IsValidTerminalWindow(hwnd))
            {
                _logger.LogWarning("ReadContent: invalid window hwnd=0x{hwnd:X}", hwnd);
                string title = GetWindowTitle(hwnd);
                return new ReadResult(hwnd.ToInt32(), title, 0, 0, string.Empty);
            }

            string currentTitle = GetWindowTitle(hwnd);

            if (!_baselines.TryGetValue(hwnd, out string[]? allLines))
            {
                // No baseline yet — capture once and establish it
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("ReadContent: hwnd=0x{hwnd:X} no baseline, capturing to establish", hwnd);

                NativeMethods.FocusWindow(hwnd);
                string? text = CaptureContent();
                if (text is null)
                {
                    _logger.LogWarning("ReadContent: capture failed for hwnd=0x{hwnd:X}", hwnd);
                    return new ReadResult(hwnd.ToInt32(), currentTitle, 0, 0, string.Empty);
                }

                allLines = TextHelper.SplitLines(text);
                _baselines[hwnd] = allLines;
            }

            int totalLines = allLines.Length;
            int actualOffset = Math.Max(1, offset);
            int actualLimit = Math.Max(1, limit);

            string[] sliced = TextHelper.SliceLines(allLines, actualOffset, actualLimit);

            // Prefix with descending line numbers (distance from bottom)
            string readText = TextHelper.BuildLinesByDescending(sliced, actualOffset);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("ReadContent: hwnd=0x{hwnd:X} totalLines={totalLines} returned={returned}", hwnd, totalLines, sliced.Length);

            return new ReadResult(hwnd.ToInt32(), currentTitle, totalLines, sliced.Length, readText);
        }

        public DiffResult ReadDiff(nint hwnd)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Per-hwnd cooldown: if called within 5s of the last diff for this window, wait
            DateTime now = DateTime.UtcNow;
            if (_diffCooldowns.TryGetValue(hwnd, out DateTime lastDiff))
            {
                int remaining = DiffCooldownMs - (int)(now - lastDiff).TotalMilliseconds;
                if (remaining > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("ReadDiff: hwnd=0x{hwnd:X} cooldown, waiting {remaining}ms", hwnd, remaining);

                    Thread.Sleep(remaining);
                }
            }

            _diffCooldowns[hwnd] = DateTime.UtcNow;

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("ReadDiff: hwnd=0x{hwnd:X}", hwnd);

            string currentTitle = GetWindowTitle(hwnd);

            if (!IsValidTerminalWindow(hwnd))
            {
                _logger.LogWarning("ReadDiff: invalid window hwnd=0x{hwnd:X}", hwnd);
                return new DiffResult(hwnd.ToInt32(), currentTitle, 0, "init", string.Empty, 0);
            }

            NativeMethods.FocusWindow(hwnd);
            string? text = CaptureContent();
            if (text is null)
            {
                _logger.LogWarning("ReadDiff: capture failed for hwnd=0x{hwnd:X}", hwnd);
                return new DiffResult(hwnd.ToInt32(), currentTitle, 0, "init", string.Empty, 0);
            }

            string[] currentLines = TextHelper.SplitLines(text);

            if (!_baselines.TryGetValue(hwnd, out string[]? previousLines))
            {
                _baselines[hwnd] = currentLines;
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("ReadDiff: hwnd=0x{hwnd:X} init baseline, lines={count}", hwnd, currentLines.Length);

                // Prefix with ascending absolute line numbers (1-based from top)
                string initText = TextHelper.BuildLines(currentLines, 1);
                return new DiffResult(hwnd.ToInt32(), currentTitle, currentLines.Length, "init",
                    initText, currentLines.Length);
            }

            DiffOutput diff = DiffCalculator.Compute(previousLines, currentLines);
            string diffText = TextHelper.BuildLines(diff.NewLines, diff.StartLine);
            int newLineCount = diff.NewLines.Length;
            string status = diff.Status == DiffStatus.New ? "new" : "no_change";

            _baselines[hwnd] = currentLines;

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("ReadDiff: hwnd=0x{hwnd:X} status={status} newLines={newCount}",
                    hwnd, status, newLineCount);

            return new DiffResult(hwnd.ToInt32(), currentTitle, currentLines.Length, status, diffText, newLineCount);
        }

        public bool IsValidTerminalWindow(nint hwnd)
        {
            if (!NativeMethods.IsWindow(hwnd))
                return false;

            string className = GetWindowClassName(hwnd);
            return className == NativeMethods.WtClassName;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _baselines.Clear();
            _windowCache.Clear();
            _diffCooldowns.Clear();

            GC.SuppressFinalize(this);
        }

        private string? CaptureContent()
        {
            // Phase 1: backup clipboard (lock-protected)
            string? clipboardBackup = null;
            _clipboardLockService.Wait();
            try
            {
                _clipboardService.TryReadText(out clipboardBackup);
            }
            finally
            {
                _clipboardLockService.Release();
            }

            // Phase 2: send keystrokes (no lock needed — keyboard input is not clipboard)
            try
            {
                // Ctrl+Shift+A (select all in Windows Terminal)
                NativeMethods.SendKeyCombo(NativeMethods.VkCtrl, NativeMethods.VkShift, NativeMethods.VkA);
                Thread.Sleep(150);

                // Ctrl+C (copy)
                NativeMethods.SendKeyCombo(NativeMethods.VkCtrl, NativeMethods.VkC);
                Thread.Sleep(150);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CaptureContent: keystroke send failed");
                RestoreClipboard(clipboardBackup);
                return null;
            }

            // Phase 3: read captured content + restore clipboard (lock-protected)
            _clipboardLockService.Wait();
            try
            {
                if (!_clipboardService.TryReadText(out var result))
                {
                    _logger.LogWarning("CaptureContent: failed to read captured content from clipboard");
                    RestoreClipboard(clipboardBackup);
                    return null;
                }

                RestoreClipboard(clipboardBackup);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CaptureContent: clipboard operation failed");
                RestoreClipboard(clipboardBackup);
                return null;
            }
            finally
            {
                _clipboardLockService.Release();
            }
        }

        private void RestoreClipboard(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                _clipboardService.TrySetText(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RestoreClipboard: failed to restore clipboard content");
            }
        }


        private static string GetWindowTitle(nint hwnd)
        {
            char[] titleBuf = new char[256];
            NativeMethods.GetWindowTextW(hwnd, titleBuf, titleBuf.Length);
            return new string(titleBuf).TrimEnd('\0');
        }

        private static string GetWindowClassName(nint hwnd)
        {
            char[] classBuf = new char[256];
            NativeMethods.GetClassNameW(hwnd, classBuf, classBuf.Length);
            return new string(classBuf).TrimEnd('\0');
        }
    }
}
