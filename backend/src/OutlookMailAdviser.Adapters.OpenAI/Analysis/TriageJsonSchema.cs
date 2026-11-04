using System.Text.Json;

namespace OutlookMailAdviser.Adapters.OpenAI.Analysis;

internal static class TriageJsonSchema
{
    public static JsonElement Value { get; } = JsonDocument.Parse(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "id": { "type": "string" },
                  "verdict": { "type": "string", "enum": ["needs_action", "review", "ignore"] },
                  "reason": { "type": "string" },
                  "priority": { "type": "string", "enum": ["low", "normal", "high", "urgent"] }
                },
                "required": ["id", "verdict", "reason", "priority"]
              }
            }
          },
          "required": ["items"]
        }
        """).RootElement.Clone();
}
