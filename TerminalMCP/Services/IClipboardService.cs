using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TerminalMCP.Services
{
    public interface IClipboardService
    {
        public bool TryReadText([MaybeNullWhen(false)] out string? result);

        public bool TrySetText(string text);
    }
}
