using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.MailContent.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Ports;

namespace OutlookMailAdviser.Adapters.MailContent;

public static class DependencyInjection
{
    public static IServiceCollection AddMailContentAdapter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<MailContentOptions>, MailContentOptionsValidator>();
        services
            .AddOptions<MailContentOptions>()
            .Bind(configuration.GetSection(MailContentOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IMailContentSanitizer, HtmlMailContentSanitizer>();

        return services;
    }
}

