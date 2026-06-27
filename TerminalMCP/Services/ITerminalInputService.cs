using System;
using System.Collections.Generic;
using System.Text;
using TerminalMCP.Models;

namespace TerminalMCP.Services
{
    public interface ITerminalInputService
    {
        public InputResult TypeText(nint hwnd, string text, bool pressEnter);

        public KeyResult SendKey(nint hwnd, string key);
    }
}
