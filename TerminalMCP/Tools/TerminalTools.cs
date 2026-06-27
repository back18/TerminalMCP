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
        public TerminalTools(
            ITerminalCaptureService captureService,
            ITerminalInputService inputService,
            ITerminalProcessService processService,
            ILogger<TerminalTools> logger)
        {
            ArgumentNullException.ThrowIfNull(captureService, nameof(captureService));
            ArgumentNullException.ThrowIfNull(inputService, nameof(inputService));
            ArgumentNullException.ThrowIfNull(processService, nameof(processService));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _captureService = captureService;
            _inputService = inputService;
            _processService = processService;
            _logger = logger;
        }

        private readonly ITerminalCaptureService _captureService;
        private readonly ITerminalInputService _inputService;
        private readonly ITerminalProcessService _processService;
        private readonly ILogger<TerminalTools> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        [McpServerTool(Name = "terminal_init")]
        [Description("Discovers Windows Terminal windows and establishes content baselines for diff tracking. New windows are captured and their baselines saved. WARNING: For new windows, this will switch window focus and use the clipboard (Ctrl+Shift+A, Ctrl+C) to capture content — avoid interacting with the terminal during this call. Call this first to discover available windows and obtain hwnd values for use with other tools. (terminal_read and terminal_diff will auto-establish baselines if none exist, but you need an hwnd to target.) If hwnd is provided, initializes only that specific window — useful after terminal_open.")]
        public string TerminalInit(
            [Description("Optional window handle to initialize a specific window. 0 (default) discovers all windows.")] int hwnd = 0)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Single-window init
                if (hwnd != 0)
                {
                    nint handle = (nint)hwnd;
                    TerminalInfo? info = _captureService.Init(handle);
                    if (info is null)
                        return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                            ErrorCodes.WindowNotTerminal,
                            $"hwnd {hwnd} is not a valid Windows Terminal window",
                            "Call terminal_init without hwnd to discover available windows"),
                            JsonOptions);

                    return JsonSerializer.Serialize(ToolResponse<TerminalInitResult>.Ok(
                        new TerminalInitResult([info]),
                        new ResponseMetadata
                        {
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                        }),
                        JsonOptions);
                }

                // Discover all windows
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
        [Description("Reads terminal content from the cached baseline (established by terminal_init, terminal_init(hwnd), or terminal_open). Falls back to a full capture (focus switch + clipboard) if no baseline exists yet. offset=1 means the last line (1-based from end).")]
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
                        "Call terminal_init to discover windows, or terminal_open to open a new one"),
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
        [Description("Captures current terminal content via clipboard (Ctrl+Shift+A, Ctrl+C), then compares against the stored baseline using line-by-line prefix matching. Auto-establishes a baseline and returns all content as 'init' if no baseline exists. Returns new or changed lines since the last baseline snapshot. The baseline is updated to the current content after each call. Status values: 'init' (first capture, no baseline exists), 'new' (new or changed lines found), 'no_change' (content unchanged). WARNING: switches window focus and uses clipboard — avoid calling while the user is typing.")]
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
                        "Call terminal_init to discover windows, or terminal_open to open a new one"),
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
        [Description("Types text into a terminal window via clipboard paste (Ctrl+V). Optionally presses Enter after pasting. WARNING: switches window focus and temporarily overwrites clipboard content.")]
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
                        "Call terminal_init to discover windows, or terminal_open to open a new one"),
                        JsonOptions);
                }

                if (string.IsNullOrEmpty(text))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.InvalidParameter,
                        "text cannot be null or empty"),
                        JsonOptions);
                }

                InputResult result = _inputService.TypeText(hwnd, text, pressEnter);

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
        [Description("Sends a single key press to a terminal window. Used for interactive prompts (confirmations, menu navigation, raw input). See parameter description for supported key names.")]
        public string TerminalKey(
            [Description("Target window handle")] int hwnd,
            [Description("Key name to press (case-insensitive): enter/return, escape/esc, tab, space, backspace, delete/del, up, down, left, right, home, end, y, n, a, c, v, d")] string key)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (!_captureService.IsValidTerminalWindow(hwnd))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.WindowNotTerminal,
                        $"hwnd {hwnd} is not a valid Windows Terminal window",
                        "Call terminal_init to discover windows, or terminal_open to open a new one"),
                        JsonOptions);
                }

                if (string.IsNullOrEmpty(key))
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.InvalidParameter,
                        "key cannot be null or empty"),
                        JsonOptions);
                }

                KeyResult result = _inputService.SendKey(hwnd, key);

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

        [McpServerTool(Name = "terminal_list_profiles")]
        [Description("Lists available Windows Terminal profiles (PowerShell, CMD, Ubuntu, VS Developer Shells, etc.) for use with terminal_open. Reads from the Windows Terminal settings.json once at startup.")]
        public string TerminalListProfiles()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                IReadOnlyList<string> profiles = _processService.GetProfiles();

                return JsonSerializer.Serialize(ToolResponse<ProfileListResult>.Ok(
                    new ProfileListResult([.. profiles]),
                    new ResponseMetadata
                    {
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    }),
                    JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "terminal_list_profiles failed");

                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.ProfileNotFound,
                    "Failed to list terminal profiles",
                    "Check that Windows Terminal is installed"),
                    JsonOptions);
            }
        }

        [McpServerTool(Name = "terminal_open")]
        [Description("Opens a new Windows Terminal window with the specified profile and optional working directory. Defaults to 'Windows PowerShell'. Discovers the new window, captures its content (WARNING: switches focus and uses clipboard), and returns the hwnd for use with terminal_read/diff/input/key. Use terminal_list_profiles to see available profile names.")]
        public string TerminalOpen(
            [Description("Profile name (from terminal_list_profiles). Defaults to 'Windows PowerShell'.")] string profile = "Windows PowerShell",
            [Description("Working directory for the new terminal")] string? workingDirectory = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (!string.IsNullOrEmpty(workingDirectory) && !Directory.Exists(workingDirectory))
            {
                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.DirectoryNotFound,
                    $"Working directory '{workingDirectory}' does not exist",
                    "Provide a valid existing directory or omit the parameter"),
                    JsonOptions);
            }

            try
            {
                nint hwnd = _processService.Open(profile, workingDirectory);
                if (hwnd == nint.Zero)
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.OpenFailed,
                        "Failed to open terminal window",
                        "The new terminal window could not be detected. Try launching Windows Terminal manually, then use terminal_init to connect"),
                        JsonOptions);
                }

                // Wait a moment for the terminal content to be ready before capturing
                Thread.Sleep(1000);

                TerminalInfo? result = _captureService.Init(hwnd);
                if (result is null)
                {
                    return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                        ErrorCodes.OpenFailed,
                        "Failed to capture new terminal window",
                        $"The new terminal window (handle: 0x{hwnd:X}) may have closed before it could be captured"),
                        JsonOptions);
                }

                OpenResult openResult = new(result.Hwnd, result.Title, result.LineCount, result.TailPreview);
                return JsonSerializer.Serialize(ToolResponse<OpenResult>.Ok(openResult, new ResponseMetadata
                {
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }),
                JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "terminal_open failed");

                return JsonSerializer.Serialize(ToolResponse<object>.Fail(
                    ErrorCodes.OpenFailed,
                    "Failed to open terminal window",
                    "An unexpected error occurred. Check inner exception for details"),
                    JsonOptions);
            }
        }
    }
}
