namespace OutlookMailAdviser.Adapters.Ollama.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ai:Ollama";

    public string BaseUrl { get; init; } = "http://127.0.0.1:11434";

    public string Model { get; init; } = "qwen3.5:4b";

    public int TimeoutSeconds { get; init; } = 180;

    public double Temperature { get; init; } = 0.1;

    public int ContextWindow { get; init; } = 4096;

    public int MaxOutputTokens { get; init; } = 512;

    public bool EnableThinking { get; init; }

    public string KeepAlive { get; init; } = "10m";
}
