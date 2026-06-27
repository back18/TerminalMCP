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

    public record ReadResult(
        [property: JsonPropertyName("hwnd")] int Hwnd,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("total_lines")] int TotalLines,
        [property: JsonPropertyName("lines_read")] int LinesRead,
        [property: JsonPropertyName("text")] string Text);

    public record DiffResult(
        [property: JsonPropertyName("hwnd")] int Hwnd,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("total_lines")] int TotalLines,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("new_line_count")] int NewLineCount);

    public record InputResult(
        [property: JsonPropertyName("success")] bool Success);

    public record KeyResult(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("key")] string Key);

    public record TerminalInitResult(
        [property: JsonPropertyName("terminals")] List<TerminalInfo> Terminals);

    public record ProfileListResult(
        [property: JsonPropertyName("profiles")] List<string> Profiles);

    public record OpenResult(
        [property: JsonPropertyName("hwnd")] int Hwnd,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("line_count")] int LineCount,
        [property: JsonPropertyName("tail_preview")] string TailPreview);
}
