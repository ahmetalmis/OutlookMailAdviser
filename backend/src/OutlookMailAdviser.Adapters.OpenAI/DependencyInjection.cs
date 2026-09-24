using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.OpenAI.Analysis;
using OutlookMailAdviser.Adapters.OpenAI.Configuration;
using OutlookMailAdviser.Adapters.OpenAI.Health;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting.Ports;

namespace OutlookMailAdviser.Adapters.OpenAI;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenAiAdapter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var environmentApiKey = configuration["OPENAI_API_KEY"];

        services.AddSingleton<IValidateOptions<OpenAiOptions>, OpenAiOptionsValidator>();
        services
            .AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    options.ApiKey = environmentApiKey ?? string.Empty;
                }
            })
            .ValidateOnStart();

        services
            .AddHttpClient<OpenAiHealthCheck>(client =>
                client.Timeout = TimeSpan.FromSeconds(10));

        services
            .AddHttpClient<OpenAiMailIntelligenceGateway>(client =>
                client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<IMailIntelligenceGateway>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenAiMailIntelligenceGateway>());
        services.AddTransient<IMailDraftGateway>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenAiMailIntelligenceGateway>());

        services
            .AddHealthChecks()
            .AddCheck<OpenAiHealthCheck>("openai", tags: ["ready"]);

        return services;
    }
}
