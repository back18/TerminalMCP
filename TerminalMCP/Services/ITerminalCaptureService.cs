using System;
using System.Collections.Generic;
using System.Text;
using TerminalMCP.Models;

namespace TerminalMCP.Services
{
    public interface ITerminalCaptureService : IDisposable
    {
        public IReadOnlyList<TerminalInfo> EnumerateWindows();

        public IReadOnlyList<TerminalInfo> Init();

        public ReadResult ReadContent(nint hwnd, int offset, int limit);

        public DiffResult ReadDiff(nint hwnd);

        public InputResult TypeText(nint hwnd, string text, bool pressEnter);

        public KeyResult SendKey(nint hwnd, string key);

        public bool IsValidTerminalWindow(nint hwnd);
    }
}
