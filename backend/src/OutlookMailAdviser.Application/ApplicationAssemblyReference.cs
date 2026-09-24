using OutlookMailAdviser.Domain;

namespace OutlookMailAdviser.Application;

/// <summary>
/// Stable marker for the application assembly.
/// </summary>
public static class ApplicationAssemblyReference
{
    public static Type DomainAssemblyMarker => typeof(DomainAssemblyReference);
}

