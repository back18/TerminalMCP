using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalMCP.Models
{
    public static class ErrorCodes
    {
        public const string WindowNotFound = "WINDOW_NOT_FOUND";

        public const string WindowNotTerminal = "WINDOW_NOT_TERMINAL";

        public const string ClipboardError = "CLIPBOARD_ERROR";

        public const string CaptureFailed = "CAPTURE_FAILED";

        public const string InputFailed = "INPUT_FAILED";

        public const string InvalidParameter = "INVALID_PARAMETER";

        public const string NoTerminalWindows = "NO_TERMINAL_WINDOWS";

        public const string OpenFailed = "OPEN_FAILED";

        public const string DirectoryNotFound = "DIRECTORY_NOT_FOUND";

        public const string ProfileNotFound = "PROFILE_NOT_FOUND";

        public const string CloseFailed = "CLOSE_FAILED";
    }
}
