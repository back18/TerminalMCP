using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TerminalMCP.Models;
using TerminalMCP.Services;

namespace TerminalMCP.Tools
{
    [McpServerToolType]
    public class TerminalTools
    {
        public TerminalTools(ITerminalCaptureService captureService, ILogger<TerminalTools> logger)
        {
            ArgumentNullException.ThrowIfNull(captureService, nameof(captureService));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _captureService = captureService;
            _logger = logger;
        }

        private readonly ITerminalCaptureService _captureService;
        private readonly ILogger<TerminalTools> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        [McpServerTool(Name = "terminal_init")]
        [Description("Discovers Windows Terminal windows and establishes content baselines for diff tracking. New windows are captured and their baselines saved; known windows only update their titles. WARNING: For new windows, this will switch window focus and use the clipboard (Ctrl+Shift+A, Ctrl+C) to capture content — avoid interacting with the terminal during this call. Call this first before terminal_diff or terminal_read.")]
        public string TerminalInit()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                IReadOnlyList<TerminalInfo> terminals = _captureService.Init();

                if (terminals.Count == 0)
                {
                    return JsonSerializer.Serialize(ToolResponse<TerminalInitResult>.Ok(
                        new TerminalInitResult([]),
                        new ResponseMetadata
                        {
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                        }),
                        JsonOptions);
                }

                List<TerminalInfo> result = [.. terminals];

                return JsonSerializer.Serialize(ToolResponse<TerminalInitResult>.Ok(
                    new TerminalInitResult(result),
                    new ResponseMetadata
                    {
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    }),
                    JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "terminal_init failed");

                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.CaptureFailed,
                    "Failed to initialize terminal windows",
                    "Ensure Windows Terminal is running"),
                    JsonOptions);
            }
        }

        [McpServerTool(Name = "terminal_read")]
        [Description("Reads terminal content from the cached baseline (established by terminal_init or a previous call). offset=1 means the last line (1-based from end). No terminal interaction — reads from memory only.")]
        public string TerminalRead(
            [Description("Target window handle")] int hwnd,
            [Description("Starting position from the last line (1=last line)")] int offset = 1,
            [Description("Maximum number of lines to return")] int limit = 20)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (!_captureService.IsValidTerminalWindow(hwnd))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.WindowNotTerminal,
                        $"hwnd {hwnd} is not a valid Windows Terminal window",
                        "Call terminal_init to discover available terminal windows"),
                        JsonOptions);
                }

                ReadResult result = _captureService.ReadContent(hwnd, offset, limit);

                return JsonSerializer.Serialize(ToolResponse<ReadResult>.Ok(result, new ResponseMetadata
                {
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }),
                JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "terminal_read failed for hwnd={hwnd}", hwnd);

                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.CaptureFailed,
                    "Failed to read terminal content",
                    "The terminal window may have been closed"),
                    JsonOptions);
            }
        }

        [McpServerTool(Name = "terminal_diff")]
        [Description("Captures current terminal content via clipboard, then compares against the stored baseline using line-by-line prefix matching. Returns only lines appended since the last baseline snapshot. The baseline is updated to the current content after each call. Status values: 'init' (first capture or baseline reset), 'new' (new lines found), 'no_change' (content unchanged).")]
        public string TerminalDiff(
            [Description("Target window handle")] int hwnd)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (!_captureService.IsValidTerminalWindow(hwnd))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.WindowNotTerminal,
                        $"hwnd {hwnd} is not a valid Windows Terminal window",
                        "Call terminal_init to discover available terminal windows"),
                        JsonOptions);
                }

                DiffResult result = _captureService.ReadDiff(hwnd);

                return JsonSerializer.Serialize(ToolResponse<DiffResult>.Ok(result, new ResponseMetadata
                {
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }),
                JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "terminal_diff failed for hwnd={hwnd}", hwnd);

                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.CaptureFailed,
                    "Failed to diff terminal content",
                    "The terminal window may have been closed"),
                    JsonOptions);
            }
        }

        [McpServerTool(Name = "terminal_input")]
        [Description("Types text into a terminal window via clipboard paste. Optionally presses Enter after pasting.")]
        public string TerminalInput(
            [Description("Target window handle")] int hwnd,
            [Description("Text to type into the terminal")] string text,
            [Description("Whether to press Enter after pasting")] bool pressEnter = true)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (!_captureService.IsValidTerminalWindow(hwnd))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.WindowNotTerminal,
                        $"hwnd {hwnd} is not a valid Windows Terminal window",
                        "Call terminal_init to discover available terminal windows"),
                        JsonOptions);
                }

                if (string.IsNullOrEmpty(text))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.InvalidParameter,
                        "text cannot be null or empty"),
                        JsonOptions);
                }

                InputResult result = _captureService.TypeText(hwnd, text, pressEnter);

                if (!result.Success)
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.InputFailed,
                        "Failed to type text into terminal window",
                        "The terminal window may have been closed or the clipboard operation failed"),
                        JsonOptions);
                }

                return JsonSerializer.Serialize(ToolResponse<InputResult>.Ok(result, new ResponseMetadata
                {
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }),
                JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "terminal_input failed for hwnd={hwnd}", hwnd);

                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.InputFailed,
                    "Failed to input text",
                    "The terminal window may have been closed"),
                    JsonOptions);
            }
        }

        [McpServerTool(Name = "terminal_key")]
        [Description("Sends a single key press to a terminal window. Supports common keys: enter, escape, tab, space, backspace, delete, up, down, left, right, home, end, y, n. Used for interactive prompts (confirmations, menu navigation).")]
        public string TerminalKey(
            [Description("Target window handle")] int hwnd,
            [Description("Key name to press (case-insensitive): enter, escape, tab, space, backspace, up, down, left, right, y, n, etc.")] string key)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (!_captureService.IsValidTerminalWindow(hwnd))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.WindowNotTerminal,
                        $"hwnd {hwnd} is not a valid Windows Terminal window",
                        "Call terminal_init to discover available terminal windows"),
                        JsonOptions);
                }

                if (string.IsNullOrEmpty(key))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.InvalidParameter,
                        "key cannot be null or empty"),
                        JsonOptions);
                }

                KeyResult result = _captureService.SendKey(hwnd, key);

                if (!result.Success)
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.InputFailed,
                        $"Failed to send key '{key}' to terminal window",
                        "Check that the key name is supported"),
                        JsonOptions);
                }

                return JsonSerializer.Serialize(ToolResponse<KeyResult>.Ok(result, new ResponseMetadata
                {
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }),
                JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "terminal_key failed for hwnd={hwnd} key='{key}'", hwnd, key);

                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.InputFailed,
                    "Failed to send key",
                    "The terminal window may have been closed"),
                    JsonOptions);
            }
        }
    }
}
