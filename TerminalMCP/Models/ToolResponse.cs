using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TerminalMCP.Models
{
    public class ToolResponse<T>
    {
        [JsonPropertyName("success")]
        public required bool Success { get; init; }

        [JsonPropertyName("data")]
        public T? Data { get; init; }

        [JsonPropertyName("error")]
        public ErrorInfo? Error { get; init; }

        [JsonPropertyName("metadata")]
        public ResponseMetadata Metadata { get; init; } = new();

        public static ToolResponse<T> Ok(T data, ResponseMetadata? metadata = null)
        {
            return new ToolResponse<T>
            {
                Success = true,
                Data = data,
                Metadata = metadata ?? new ResponseMetadata()
            };
        }

        public static ToolResponse<T> Fail(string code, string message, string? suggestion = null, bool recoverable = true)
        {
            return new ToolResponse<T>
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = code,
                    Message = message,
                    Suggestion = suggestion,
                    Recoverable = recoverable
                }
            };
        }
    }
}
