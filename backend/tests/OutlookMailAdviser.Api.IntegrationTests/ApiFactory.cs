using OutlookMailAdviser.Adapters.ActionNotes;
using OutlookMailAdviser.Application.ActionNotes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting.Ports;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string actionDataDirectory = Path.Combine(Path.GetTempPath(), "OutlookMailAdviser-api-tests", Guid.NewGuid().ToString("N"));
    public StubMailIntelligenceGateway IntelligenceGateway { get; } = new();

    public StubMailQuestionGateway QuestionGateway { get; } = new();

    public StubMailDraftGateway DraftGateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Provider"] = "Ollama"
            }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IActionNoteRepository>();
            services.AddSingleton<IActionNoteRepository>(_ => new JsonActionNoteRepository(actionDataDirectory));
            services.RemoveAll<IMailIntelligenceGateway>();
            services.AddSingleton<IMailIntelligenceGateway>(IntelligenceGateway);
            services.RemoveAll<OutlookMailAdviser.Application.MailQuestions.IMailQuestionGateway>();
            services.AddSingleton<OutlookMailAdviser.Application.MailQuestions.IMailQuestionGateway>(QuestionGateway);
            services.RemoveAll<IMailDraftGateway>();
            services.AddSingleton<IMailDraftGateway>(DraftGateway);
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(actionDataDirectory))
        {
            var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OutlookMailAdviser-api-tests")) + Path.DirectorySeparatorChar;
            var resolved = Path.GetFullPath(actionDataDirectory);
            if (!resolved.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test directory.");
            Directory.Delete(resolved, recursive: true);
        }
    }

}
