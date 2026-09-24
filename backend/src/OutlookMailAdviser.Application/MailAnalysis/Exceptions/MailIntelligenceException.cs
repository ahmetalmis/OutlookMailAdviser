namespace OutlookMailAdviser.Application.MailAnalysis.Exceptions;

public abstract class MailIntelligenceException : Exception
{
    protected MailIntelligenceException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

