using System.Text.Json;

namespace OutlookMailAdviser.Adapters.Ollama.Analysis;

internal static class DraftJsonSchema
{
    public static readonly JsonElement Value = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "subject": { "type": "string" },
            "body": { "type": "string" }
          },
          "required": ["subject", "body"]
        }
        """);
}
