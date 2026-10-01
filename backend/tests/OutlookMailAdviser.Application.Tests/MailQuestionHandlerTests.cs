using System.Text;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;
using OutlookMailAdviser.Application.MailQuestions;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.Tests;

public sealed class MailQuestionHandlerTests
{
    [Fact]
    public async Task FindsEvidenceAtTheEndOfLongHistory()
    {
        var body = string.Concat(Enumerable.Repeat("Yeni mesaj içeriği.\n", 1200)) + "Teslim 12 Ekim.";
        var gateway = new FakeGateway((content, _) => Task.FromResult(content.Contains("Teslim 12 Ekim.", StringComparison.Ordinal)
            ? new QuestionFinding("Teslim 12 Ekim.", ["Teslim 12 Ekim."])
            : new QuestionFinding("", [])));
        var result = await new MailQuestionHandler(new Reader(), gateway)
            .ExecuteAsync(Message(body), "Teslim ne zaman?", "tr", CancellationToken.None);
        Assert.False(result.IsPartial);
        Assert.Equal(result.TotalChunks, result.CompletedChunks);
        Assert.Contains(result.Sources, source => source.Quote == "Teslim 12 Ekim.");
        Assert.True(gateway.Calls > 2);
    }

    [Fact]
    public async Task RejectsInventedQuotesWithoutClaimingAbsence()
    {
        var handler = new MailQuestionHandler(new Reader(), new FakeGateway((_, _) =>
            Task.FromResult(new QuestionFinding("Onaylandı", ["Olmayan bilgi"]))));
        var result = await handler.ExecuteAsync(Message("Gerçek içerik"), "Onay?", "tr", CancellationToken.None);
        Assert.True(result.IsPartial);
        Assert.Empty(result.Sources);
        Assert.Contains("tamamlanamadı", result.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnsNotFoundOnlyAfterExaminingAllChunks()
    {
        var handler = new MailQuestionHandler(new Reader(), new FakeGateway((_, _) =>
            Task.FromResult(new QuestionFinding("", []))));
        var result = await handler.ExecuteAsync(Message(new string('x', 5000)), "Tarih?", "tr", CancellationToken.None);
        Assert.False(result.IsPartial);
        Assert.Equal(result.TotalChunks, result.CompletedChunks);
        Assert.Contains("bulunamadı", result.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderFailureRetainsEarlierVerifiedFindings()
    {
        var calls = 0;
        var handler = new MailQuestionHandler(new Reader(), new FakeGateway((_, _) =>
            ++calls == 1 ? Task.FromResult(new QuestionFinding("Bilgi", ["Bilgi"]))
                : throw new ModelProviderException("PRIVATE_SENTINEL", "openai_quota_exceeded")));
        var result = await handler.ExecuteAsync(Message("Bilgi\n" + new string('x', 5000)), "Soru", "tr", CancellationToken.None);
        Assert.True(result.IsPartial);
        Assert.Single(result.Sources);
        Assert.Equal(1, result.CompletedChunks);
        Assert.Equal("openai_quota_exceeded", result.ErrorCode);
        Assert.DoesNotContain("PRIVATE_SENTINEL", string.Join(" ", result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitialAuthenticationFailureIsVisibleWithoutLeakingTheProviderMessage()
    {
        var handler = new MailQuestionHandler(new Reader(), new FakeGateway((_, _) =>
            throw new ModelProviderException("PRIVATE_SENTINEL", "openai_authentication_failed")));
        var result = await handler.ExecuteAsync(Message("Synthetic content"), "Soru", "tr", CancellationToken.None);
        Assert.Equal("openai_authentication_failed", result.ErrorCode);
        Assert.Equal(0, result.CompletedChunks);
        Assert.True(result.IsPartial);
        Assert.DoesNotContain("PRIVATE_SENTINEL", result.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new MailQuestionHandler(new Reader(), new FakeGateway((_, token) =>
            Task.FromCanceled<QuestionFinding>(token)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(Message("İçerik"), "Soru", "tr", cancellation.Token));
    }

    [Fact]
    public void ChunksCoverEveryCharacterAndRespectUtf8Budget()
    {
        var body = string.Concat(Enumerable.Repeat("Türkçe 👩‍💻 satır\n", 500));
        var chunks = MailQuestionHandler.Split(body, 700);
        var coverage = new bool[body.Length];
        foreach (var (start, text) in chunks)
        {
            Assert.True(Encoding.UTF8.GetByteCount(text) <= 700);
            Assert.Equal(text, body.Substring(start, text.Length));
            Assert.False(char.IsLowSurrogate(text[0]));
            Assert.False(char.IsHighSurrogate(text[^1]));
            Array.Fill(coverage, true, start, text.Length);
        }
        Assert.All(coverage, Assert.True);
    }

    [Fact]
    public async Task CombinesConflictingEvidenceAcrossChunks()
    {
        const string oldDate = "Eski tarih 12 Ekim.";
        const string newDate = "Yeni tarih 14 Ekim.";
        var compared = false;
        var handler = new MailQuestionHandler(new Reader(), new FakeGateway((content, _) =>
        {
            if (content.Contains(oldDate, StringComparison.Ordinal) && content.Contains(newDate, StringComparison.Ordinal))
            {
                compared = true;
                return Task.FromResult(new QuestionFinding("Tarih 12 Ekim'den 14 Ekim'e değişmiş.", [oldDate, newDate]));
            }
            return Task.FromResult(content.Contains(oldDate, StringComparison.Ordinal)
                ? new QuestionFinding(oldDate, [oldDate])
                : content.Contains(newDate, StringComparison.Ordinal)
                    ? new QuestionFinding(newDate, [newDate]) : new QuestionFinding("", []));
        }));
        var result = await handler.ExecuteAsync(Message(oldDate + new string('x', 3000) + newDate),
            "Tarih?", "tr", CancellationToken.None);
        Assert.True(compared);
        Assert.False(result.IsPartial);
        Assert.Equal(2, result.Sources.Count);
        Assert.Contains("değişmiş", result.Answer, StringComparison.Ordinal);
    }

    private static MailMessage Message(string body) => new(null, "Test", new(null, "a@example.com"),
        [new(null, "b@example.com")], [], null, body, MailBodyFormat.PlainText);

    private sealed class Reader : IFullMailContentReader
    {
        public string ReadFullBody(MailMessage message) => message.Body;
    }

    private sealed class FakeGateway(Func<string, CancellationToken, Task<QuestionFinding>> answer) : IMailQuestionGateway
    {
        public int Calls { get; private set; }
        public int GetContentByteBudget(string question, string language) => 1200;
        public Task<QuestionFinding> AnswerAsync(string content, string question, string language, CancellationToken cancellationToken)
        {
            Calls++;
            return answer(content, cancellationToken);
        }
    }
}
