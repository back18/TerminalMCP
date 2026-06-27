using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalMCP.Models
{
    public record DiffOutput
    {
        public required DiffStatus Status { get; init; }

        public required string[] NewLines { get; init; }

        public required int StartLine { get; init; }
    }
}
