using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting.Ports;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public StubMailIntelligenceGateway IntelligenceGateway { get; } = new();

    public StubMailDraftGateway DraftGateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMailIntelligenceGateway>();
            services.AddSingleton<IMailIntelligenceGateway>(IntelligenceGateway);
            services.RemoveAll<IMailDraftGateway>();
            services.AddSingleton<IMailDraftGateway>(DraftGateway);
        });
    }
}
