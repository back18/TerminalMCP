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

        public TerminalInfo? Init(nint hwnd);

        public ReadResult ReadContent(nint hwnd, int offset, int limit);

        public DiffResult ReadDiff(nint hwnd);

        public bool IsValidTerminalWindow(nint hwnd);
    }
}
