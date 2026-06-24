using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TerminalMCP.Models
{
    public class ErrorInfo
    {
        [JsonPropertyName("code")]
        public required string Code { get; init; }

        [JsonPropertyName("message")]
        public required string Message { get; init; }

        [JsonPropertyName("suggestion")]
        public string? Suggestion { get; init; }

        [JsonPropertyName("recoverable")]
        public bool Recoverable { get; init; } = true;
    }
}
