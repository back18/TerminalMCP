using System;
using System.Collections.Generic;
using System.Text;
using TerminalMCP.Models;

namespace TerminalMCP.Services
{
    public interface ITerminalProcessService
    {
        public IReadOnlyList<string> GetProfiles();

        public nint Open(string profileName, string? workingDirectory);

        public bool CloseTerminal(nint hwnd);
    }
}
