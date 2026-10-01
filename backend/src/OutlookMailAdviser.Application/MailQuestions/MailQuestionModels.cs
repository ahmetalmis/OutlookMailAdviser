using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailQuestions;

public interface IFullMailContentReader
{
    string ReadFullBody(MailMessage message);
}

public interface IMailQuestionGateway
{
    int GetContentByteBudget(string question, string language);

    Task<QuestionFinding> AnswerAsync(string content, string question, string language,
        CancellationToken cancellationToken);
}

public sealed record QuestionFinding(string Answer, IReadOnlyList<string> Quotes);

public sealed record QuestionSource(string Id, string Quote, int StartOffset);

public sealed record MailQuestionResult(string Answer, IReadOnlyList<QuestionSource> Sources,
    int CompletedChunks, int TotalChunks, bool IsPartial, IReadOnlyList<string> Warnings, string? ErrorCode = null);

public interface IAskMailQuestionUseCase
{
    Task<MailQuestionResult> ExecuteAsync(MailMessage message, string question, string language,
        CancellationToken cancellationToken);
}
