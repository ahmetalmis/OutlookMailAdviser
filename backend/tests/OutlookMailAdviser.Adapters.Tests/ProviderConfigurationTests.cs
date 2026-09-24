using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.OpenAI;
using OutlookMailAdviser.Adapters.OpenAI.Configuration;

namespace OutlookMailAdviser.Adapters.Tests;

public sealed class ProviderConfigurationTests
{
    [Fact]
    public void MissingOpenAiKeyHasActionableValidationMessage()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["OPENAI_API_KEY"] = "" }).Build();
        var services = new ServiceCollection();
        services.AddOpenAiAdapter(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenAiOptions>>();
        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("Set OPENAI_API_KEY", exception.Message, StringComparison.Ordinal);
    }
}
