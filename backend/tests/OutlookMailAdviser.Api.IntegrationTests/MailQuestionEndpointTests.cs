using System.Net;
using System.Net.Http.Json;
using OutlookMailAdviser.Application.MailQuestions;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class StubMailQuestionGateway : IMailQuestionGateway
{
    public int GetContentByteBudget(string question, string language) => 1000;
    public Task<QuestionFinding> AnswerAsync(string content, string question, string language, CancellationToken cancellationToken) =>
        Task.FromResult(content.Contains("Teslim 12 Ekim.", StringComparison.Ordinal)
            ? new QuestionFinding("Teslim 12 Ekim.", ["Teslim 12 Ekim."])
            : new QuestionFinding("", []));
}

public sealed class MailQuestionEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object Message(string body) => new
    {
        subject = "Test",
        from = new { address = "a@example.com" },
        to = new[] { new { address = "b@example.com" } },
        body,
        bodyFormat = "html"
    };

    [Fact]
    public async Task QuestionsFindOldHistoryBeyondNormalAnalysisLimit()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/mail/questions", new
        {
            message = Message("<p>" + new string('x', 18000) + "</p><blockquote>Teslim 12 Ekim.</blockquote>"),
            question = "Teslim ne zaman?",
            preferredLanguage = "tr"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MailQuestionResult>();
        Assert.NotNull(result);
        Assert.False(result.IsPartial);
        Assert.Contains(result.Sources, source => source.Quote == "Teslim 12 Ekim.");
    }

    [Fact]
    public async Task QuestionsRejectMissingQuestion()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/mail/questions", new { message = Message("Test") });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Teslim 12 Ekim.", HttpStatusCode.OK)]
    [InlineData("Teslim 13 Ekim.", HttpStatusCode.BadRequest)]
    public async Task DraftAcceptsOnlyQuotesFromCurrentBody(string quote, HttpStatusCode status)
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/mail/draft", new
        {
            message = Message("<p>Teslim 12 Ekim.</p>"),
            instructions = "Tarihi teyit et.",
            tone = "professional",
            sourceQuotes = new[] { quote }
        });
        Assert.Equal(status, response.StatusCode);
        if (status == HttpStatusCode.OK)
        {
            Assert.Contains(quote, factory.DraftGateway.ReceivedConversation?.Content, StringComparison.Ordinal);
        }
    }
}
