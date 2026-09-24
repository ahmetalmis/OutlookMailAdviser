using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailAnalysis.Ports;

public interface IMailContentSanitizer
{
    SanitizedConversation Sanitize(MailConversation conversation);
}

