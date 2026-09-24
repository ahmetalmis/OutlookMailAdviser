using System.Text.Json;

namespace OutlookMailAdviser.Adapters.OpenAI.Analysis;

internal static class AnalysisJsonSchema
{
    public static JsonElement Value { get; } = JsonDocument.Parse(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "summary": { "type": "string", "minLength": 1 },
            "actionRequired": { "type": "boolean" },
            "actions": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "description": { "type": "string", "minLength": 1 },
                  "owner": { "type": ["string", "null"] },
                  "dueDate": {
                    "type": ["string", "null"],
                    "pattern": "^\\d{4}-\\d{2}-\\d{2}$",
                    "description": "Calendar date only in yyyy-MM-dd format."
                  },
                  "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
                  "assignedToCurrentUser": { "type": "boolean" }
                },
                "required": ["description", "owner", "dueDate", "confidence", "assignedToCurrentUser"]
              }
            },
            "priority": { "type": "string", "enum": ["low", "normal", "high", "urgent"] },
            "sentiment": { "type": "string", "enum": ["negative", "neutral", "positive", "mixed"] },
            "warnings": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["summary", "actionRequired", "actions", "priority", "sentiment", "warnings"]
        }
        """).RootElement.Clone();
}
