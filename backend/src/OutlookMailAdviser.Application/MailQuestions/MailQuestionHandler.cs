using System.Globalization;
using System.Text;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailQuestions;

public sealed class MailQuestionHandler(IFullMailContentReader reader, IMailQuestionGateway gateway)
    : IAskMailQuestionUseCase
{
    public async Task<MailQuestionResult> ExecuteAsync(MailMessage message, string question,
        string language, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(question) || question.Length > 1000)
        {
            throw new ArgumentException("Question must contain between 1 and 1000 characters.", nameof(question));
        }
        if (string.IsNullOrWhiteSpace(language)) { language = "tr"; }
        if (language.Length > 15) { throw new ArgumentException("Invalid language.", nameof(language)); }
        _ = CultureInfo.GetCultureInfo(language);
        var turkish = language.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        var content = reader.ReadFullBody(message);
        var chunks = Split(content, gateway.GetContentByteBudget(question, language));
        var sources = new List<QuestionSource>();
        var answers = new List<string>();
        var warnings = new List<string>();
        var completed = 0;
        string? providerErrorCode = null;
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var finding = await gateway.AnswerAsync(chunk.Text, question, language, cancellationToken);
                if (finding.Quotes is null || finding.Quotes.Any(quote => string.IsNullOrWhiteSpace(quote)
                    || !chunk.Text.Contains(quote, StringComparison.Ordinal))
                    || (finding.Quotes.Count > 0 && string.IsNullOrWhiteSpace(finding.Answer)))
                {
                    throw new InvalidModelResponseException();
                }
                var ids = new List<string>();
                foreach (var quote in finding.Quotes.Distinct(StringComparer.Ordinal))
                {
                    var offset = chunk.Start + chunk.Text.IndexOf(quote, StringComparison.Ordinal);
                    var id = $"source-{offset}-{quote.Length}";
                    if (!sources.Any(source => source.Id == id)) { sources.Add(new(id, quote, offset)); }
                    ids.Add(id);
                }
                if (ids.Count > 0)
                {
                    answers.Add(finding.Answer);
                }
                completed++;
            }
            catch (MailIntelligenceException exception)
            {
                providerErrorCode = ProviderErrorDetails.NormalizeCode(exception.Code);
                // Do not turn an unavailable provider or an unverified answer into "not found".
                warnings.Add(turkish
                    ? "Bir bölüm incelenemedi veya kaynak alıntısı doğrulanamadı. Sonuç kısmidir."
                    : "A section could not be examined or its quotation could not be verified. Results are partial.");
                break;
            }
        }

        var answer = answers.Count == 0
            ? (completed == chunks.Count
                ? (turkish ? "Bu e-postanın içindeki yazışmalarda bulunamadı." : "Not found in the correspondence inside this email.")
                : (turkish ? "İnceleme tamamlanamadı; bilgi bulunmadığı sonucuna varılamaz." : "The review is incomplete; absence of information cannot be established."))
            : string.Join("\n\n", answers.Distinct(StringComparer.Ordinal));

        // Compare evidence across chunks when it fits; never silently discard sources to fit a model.
        var evidence = string.Join("\n\n", sources.Select(source => source.Quote));
        if (answers.Count > 1 && Encoding.UTF8.GetByteCount(evidence) <= gateway.GetContentByteBudget(question, language))
        {
            try
            {
                var synthesis = await gateway.AnswerAsync(evidence, question, language, cancellationToken);
                if (synthesis.Quotes.Count > 0 && !string.IsNullOrWhiteSpace(synthesis.Answer)
                    && synthesis.Quotes.All(quote => !string.IsNullOrWhiteSpace(quote)
                        && sources.Any(source => source.Quote.Contains(quote, StringComparison.Ordinal))))
                {
                    answer = synthesis.Answer;
                }
                else { throw new InvalidModelResponseException(); }
            }
            catch (MailIntelligenceException exception)
            {
                providerErrorCode = ProviderErrorDetails.NormalizeCode(exception.Code);
                warnings.Add(turkish ? "Birleşik yanıt hazırlanamadı; bölüm bulguları gösteriliyor."
                    : "A combined answer could not be prepared; section findings are shown.");
            }
        }
        else if (answers.Count > 1)
        {
            warnings.Add(turkish
                ? "Bulgular bölüm bölüm gösteriliyor; bölümler arası çelişkiler otomatik karşılaştırılamadı."
                : "Findings are shown by section; conflicts across sections could not be compared automatically.");
        }
        return new(answer, sources, completed, chunks.Count, completed != chunks.Count,
            warnings.Distinct(StringComparer.Ordinal).ToArray(), providerErrorCode);
    }

    public static IReadOnlyList<(int Start, string Text)> Split(string content, int byteBudget)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (byteBudget < 256) { throw new ArgumentException("Model context is too small for this question.", nameof(byteBudget)); }
        var chunks = new List<(int Start, string Text)>();
        var start = 0;
        while (start < content.Length)
        {
            var end = start;
            var bytes = 0;
            foreach (var rune in content.AsSpan(start).EnumerateRunes())
            {
                if (bytes + rune.Utf8SequenceLength > byteBudget) { break; }
                bytes += rune.Utf8SequenceLength;
                end += rune.Utf16SequenceLength;
            }
            if (end < content.Length)
            {
                var newline = content.LastIndexOf('\n', end - 1, end - start);
                if (newline > start + (end - start) * 3 / 4) { end = newline + 1; }
            }
            chunks.Add((start, content[start..end]));
            if (end == content.Length) { break; }
            var next = end - Math.Min(128, (end - start) / 4);
            if (char.IsLowSurrogate(content[next])) { next++; }
            start = next;
        }
        return chunks;
    }
}
