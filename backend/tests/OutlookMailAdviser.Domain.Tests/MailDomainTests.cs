using OutlookMailAdviser.Domain.Analysis;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Domain.Tests;

public sealed class MailDomainTests
{
    [Fact]
    public void MailActionKeepsCurrentUserAssignment()
    {
        var action = new MailAction(
            "Tarihi teyit et",
            "Ali",
            null,
            0.9,
            assignedToCurrentUser: true);

        Assert.True(action.AssignedToCurrentUser);
    }

    [Fact]
    public void MailMessageRequiresAtLeastOneRecipient()
    {
        Assert.Throws<ArgumentException>(() => new MailMessage(
            null,
            "Subject",
            new MailParticipant(null, "sender@example.com"),
            [],
            [],
            null,
            "Body",
            MailBodyFormat.PlainText));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void MailActionRejectsConfidenceOutsideUnitInterval(double confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MailAction(
            "Confirm the date",
            null,
            null,
            confidence));
    }
}
