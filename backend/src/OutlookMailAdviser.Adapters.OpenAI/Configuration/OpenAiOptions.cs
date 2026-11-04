namespace OutlookMailAdviser.Adapters.OpenAI.Configuration;

public sealed class OpenAiOptions
{
    public const string SectionName = "Ai:OpenAI";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4.1-mini";

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxOutputTokens { get; set; } = 512;

    public double Temperature { get; set; } = 0.1;
}
