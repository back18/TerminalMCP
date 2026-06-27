using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TerminalMCP.Models
{
    public record TerminalInfo(
        [property: JsonPropertyName("hwnd")] int Hwnd,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("line_count")] int LineCount,
        [property: JsonPropertyName("tail_preview")] string TailPreview);
}
