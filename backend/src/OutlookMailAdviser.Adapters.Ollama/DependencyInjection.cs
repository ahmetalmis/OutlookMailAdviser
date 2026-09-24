using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.Ollama.Analysis;
using OutlookMailAdviser.Adapters.Ollama.Configuration;
using OutlookMailAdviser.Adapters.Ollama.Health;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting.Ports;

namespace OutlookMailAdviser.Adapters.Ollama;

public static class DependencyInjection
{
    public static IServiceCollection AddOllamaAdapter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<OllamaOptions>, OllamaOptionsValidator>();
        services
            .AddOptions<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName))
            .ValidateOnStart();

        services
            .AddHttpClient<OllamaHealthCheck>(client =>
                client.Timeout = TimeSpan.FromSeconds(10));

        services
            .AddHttpClient<OllamaMailIntelligenceGateway>(client =>
                client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<IMailIntelligenceGateway>(serviceProvider =>
            serviceProvider.GetRequiredService<OllamaMailIntelligenceGateway>());
        services.AddTransient<IMailDraftGateway>(serviceProvider =>
            serviceProvider.GetRequiredService<OllamaMailIntelligenceGateway>());

        services
            .AddHealthChecks()
            .AddCheck<OllamaHealthCheck>("ollama", tags: ["ready"]);

        return services;
    }
}
