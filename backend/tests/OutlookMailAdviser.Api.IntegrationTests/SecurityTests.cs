using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Application.MailDrafting.Ports;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class SecurityTests
{
    [Theory]
    [InlineData("https://localhost:3000", true)]
    [InlineData("https://untrusted.example.com", false)]
    [InlineData("https://localhost:3000.untrusted.example.com", false)]
    public async Task CorsOnlyAllowsConfiguredOrigin(string origin, bool allowed)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/mail/analysis");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }
    }

    [Theory]
    [InlineData("/api/v1/mail/analysis", "model_provider_error")]
    [InlineData("/api/v1/mail/draft", "openai_unavailable")]
    public async Task ProviderFailureDoesNotExposePrivateDetails(string endpoint, string code)
    {
        await using var factory = new ApiFactory();
        using var logs = new CapturedLogs();
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                var gateway = new FailingGateway(code);
                services.RemoveAll<IMailIntelligenceGateway>();
                services.RemoveAll<IMailDraftGateway>();
                services.AddSingleton<IMailIntelligenceGateway>(gateway);
                services.AddSingleton<IMailDraftGateway>(gateway);
            });
        });
        using var client = failingFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(endpoint, new
        {
            preferredLanguage = "tr",
            instructions = "Teşekkür et.",
            tone = "professional",
            message = new
            {
                subject = "Sentetik test",
                from = new { name = "Deniz", address = "deniz@example.com" },
                to = new[] { new { name = "Eren", address = "eren@example.com" } },
                body = "Yarın planı paylaşır mısın?",
                bodyFormat = "plainText"
            }
        });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PRIVATE_SENTINEL", body, StringComparison.Ordinal);
        Assert.Contains(code, body, StringComparison.Ordinal);
        Assert.Contains(logs.Messages, message => message.Contains(code, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("PRIVATE_SENTINEL", StringComparison.Ordinal));
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturedLogger(Messages);

        public void Dispose() { }

        private sealed class CapturedLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + exception);
        }
    }

    private sealed class FailingGateway(string code) : IMailIntelligenceGateway, IMailDraftGateway
    {
        public Task<MailIntelligenceResult> AnalyzeAsync(
            SanitizedConversation conversation, AnalysisOptions options, CancellationToken cancellationToken) =>
            throw new ModelProviderException("PRIVATE_SENTINEL: message body, credential and remote URL", code);

        public Task<MailDraftGatewayResult> GenerateDraftAsync(
            SanitizedConversation conversation, DraftOptions options, CancellationToken cancellationToken) =>
            throw new ModelProviderException("PRIVATE_SENTINEL: message body, credential and remote URL", code);
    }
}
