using OutlookMailAdviser.Application;
using OutlookMailAdviser.Domain;

namespace OutlookMailAdviser.Adapters.MailContent;

/// <summary>
/// Stable marker for the mail-content adapter assembly.
/// </summary>
public static class MailContentAssemblyReference
{
    public static Type ApplicationAssemblyMarker => typeof(ApplicationAssemblyReference);

    public static Type DomainAssemblyMarker => typeof(DomainAssemblyReference);
}

