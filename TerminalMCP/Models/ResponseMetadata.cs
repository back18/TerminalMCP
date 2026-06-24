using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TerminalMCP.Models
{
    public class ResponseMetadata
    {
        [JsonPropertyName("execution_time_ms")]
        public long ExecutionTimeMs { get; set; }

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; init; } = [];
    }
}
