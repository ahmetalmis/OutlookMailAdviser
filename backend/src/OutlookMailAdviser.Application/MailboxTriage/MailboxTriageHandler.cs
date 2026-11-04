using System.Globalization;
using OutlookMailAdviser.Application.MailboxTriage.Models;
using OutlookMailAdviser.Application.MailboxTriage.Ports;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailboxTriage;

public sealed class MailboxTriageHandler(IMailboxPreviewClassifier classifier) : IMailboxTriageUseCase
{
    public const int MaxClassified = 60;

    public const int BatchSize = 12;

    public const int MaxActionItems = 20;

    public const int MaxReviewItems = 30;

    public async Task<MailboxTriageResult> ExecuteAsync(
        MailboxTriageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Messages);

        var preferredLanguage = NormalizeLanguage(command.PreferredLanguage);
        var passed = command.Messages
            .Where(message => !MailboxHeaderFilter.ShouldDrop(message, command.CurrentUser))
            .OrderByDescending(message => message.ReceivedAt ?? DateTimeOffset.MinValue)
            .ToArray();
        var headerFilteredCount = command.Messages.Count - passed.Length;
        var kept = passed.Take(MaxClassified).ToArray();

        if (kept.Length == 0)
        {
            return new MailboxTriageResult(
                command.Messages.Count,
                headerFilteredCount,
                0,
                string.Empty,
                0,
                [],
                []);
        }

        var verdicts = new List<MailboxPreviewVerdict>();
        var model = string.Empty;
        long duration = 0;

        for (var offset = 0; offset < kept.Length; offset += BatchSize)
        {
            var batch = kept.Skip(offset).Take(BatchSize).ToArray();
            var aliased = batch
                .Select((message, index) => ForModel(message, $"m{index + 1}"))
                .ToArray();
            var classification = await classifier.ClassifyAsync(
                aliased,
                command.CurrentUser,
                preferredLanguage,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(classification.Model))
            {
                model = classification.Model;
            }

            duration += Math.Max(0, classification.DurationMilliseconds);
            verdicts.AddRange(MapVerdicts(batch, classification.Verdicts));
        }

        var byId = kept.ToDictionary(message => message.ItemId, StringComparer.Ordinal);
        var actionItems = new List<MailboxTriageItem>();
        var reviewItems = new List<MailboxTriageItem>();

        foreach (var verdict in verdicts)
        {
            if (!byId.TryGetValue(verdict.ItemId, out var message))
            {
                continue;
            }

            var itemVerdict = PromoteDirectRequest(message, verdict, command.CurrentUser);
            var item = ToItem(message, itemVerdict);
            if (itemVerdict.Verdict == MailboxVerdict.NeedsAction && actionItems.Count < MaxActionItems)
            {
                actionItems.Add(item);
                continue;
            }

            if (itemVerdict.Verdict is MailboxVerdict.Review or MailboxVerdict.NeedsAction
                && reviewItems.Count < MaxReviewItems)
            {
                reviewItems.Add(item);
            }
        }

        actionItems.Sort(CompareItems);
        reviewItems.Sort(CompareItems);

        return new MailboxTriageResult(
            command.Messages.Count,
            headerFilteredCount,
            kept.Length,
            model,
            duration,
            actionItems,
            reviewItems);
    }

    private static MailboxPreviewVerdict PromoteDirectRequest(
        MailboxPreviewMessage message,
        MailboxPreviewVerdict verdict,
        MailParticipant? currentUser)
    {
        if (currentUser is null || verdict.Verdict == MailboxVerdict.NeedsAction)
        {
            return verdict;
        }

        if (!MailboxHeaderFilter.IsAddressedTo(message, currentUser))
        {
            return verdict;
        }

        if (!HasInformation(message) && !AsksForPersonalReply(message))
        {
            return verdict;
        }

        return verdict with
        {
            Verdict = MailboxVerdict.NeedsAction,
            Priority = "normal",
            Reason = string.IsNullOrWhiteSpace(verdict.Reason)
                ? "Size yazılmış bir bilgi var."
                : verdict.Reason
        };
    }

    private static bool HasInformation(MailboxPreviewMessage message)
    {
        var preview = message.Preview.Trim();
        return preview.Length >= 40;
    }

    private static bool AsksForPersonalReply(MailboxPreviewMessage message)
    {
        var text = string.Concat(message.Subject, "\n", message.Preview)
            .ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        if (text.Contains('?', StringComparison.Ordinal))
        {
            return true;
        }

        string[] cues =
        [
            "ne yapaca",
            "ne yapal",
            "ne yapay",
            "ne dersin",
            "ne düşün",
            "bakar mıs",
            "müsaits",
            "müsait mi",
            "dönüş yap",
            "cevap bek",
            "yanıt bek",
            "rica et",
            "lütfen",
            "büyült",
            "küçült",
            "olsun",
            "yaparsan",
            "edersen",
            "gönderir mis",
            "iletir mis"
        ];
        return cues.Any(cue => text.Contains(cue, StringComparison.Ordinal));
    }

    private static MailboxPreviewMessage ForModel(MailboxPreviewMessage message, string alias) =>
        message with
        {
            ItemId = alias,
            Subject = Trim(message.Subject, 180),
            Preview = Trim(message.Preview, 280),
            To = message.To.Take(4).ToArray(),
            Cc = message.Cc.Take(4).ToArray()
        };

    private static List<MailboxPreviewVerdict> MapVerdicts(
        IReadOnlyList<MailboxPreviewMessage> batch,
        IReadOnlyList<MailboxPreviewVerdict> verdicts)
    {
        var mapped = new List<MailboxPreviewVerdict>(batch.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var verdict in verdicts)
        {
            if (string.IsNullOrWhiteSpace(verdict.ItemId)
                || verdict.ItemId.Length < 2
                || char.ToLowerInvariant(verdict.ItemId[0]) != 'm'
                || !int.TryParse(verdict.ItemId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                || index < 1
                || index > batch.Count)
            {
                continue;
            }

            var message = batch[index - 1];
            if (!seen.Add(message.ItemId))
            {
                continue;
            }

            mapped.Add(verdict with { ItemId = message.ItemId });
        }

        foreach (var message in batch)
        {
            if (seen.Contains(message.ItemId))
            {
                continue;
            }

            mapped.Add(new MailboxPreviewVerdict(
                message.ItemId,
                MailboxVerdict.Review,
                "Önizleme sonucu belirsiz.",
                "normal"));
        }

        return mapped;
    }

    private static MailboxTriageItem ToItem(MailboxPreviewMessage message, MailboxPreviewVerdict verdict) =>
        new(
            message.ItemId,
            message.Subject,
            message.From.Name,
            message.From.Address,
            message.ReceivedAt,
            string.IsNullOrWhiteSpace(verdict.Reason) ? "Kontrol edin." : verdict.Reason.Trim(),
            NormalizePriority(verdict.Priority));

    private static int CompareItems(MailboxTriageItem left, MailboxTriageItem right)
    {
        var priority = PriorityRank(left.Priority).CompareTo(PriorityRank(right.Priority));
        if (priority != 0)
        {
            return priority;
        }

        return (right.ReceivedAt ?? DateTimeOffset.MinValue).CompareTo(left.ReceivedAt ?? DateTimeOffset.MinValue);
    }

    private static int PriorityRank(string priority) => priority switch
    {
        "urgent" => 0,
        "high" => 1,
        "normal" => 2,
        _ => 3
    };

    private static string NormalizePriority(string priority) => priority.Trim().ToLowerInvariant() switch
    {
        "urgent" => "urgent",
        "high" => "high",
        "low" => "low",
        _ => "normal"
    };

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string NormalizeLanguage(string preferredLanguage)
    {
        if (string.IsNullOrWhiteSpace(preferredLanguage))
        {
            return "tr";
        }

        var normalized = preferredLanguage.Trim();
        if (normalized.Length > 15)
        {
            throw new ArgumentException(
                "Preferred language cannot exceed 15 characters.",
                nameof(preferredLanguage));
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(normalized);
        }
        catch (CultureNotFoundException exception)
        {
            throw new ArgumentException(
                "Preferred language must be a valid culture name such as 'tr' or 'en-US'.",
                nameof(preferredLanguage),
                exception);
        }

        return normalized;
    }
}
