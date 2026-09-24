using OutlookMailAdviser.Application;
using OutlookMailAdviser.Domain;

namespace OutlookMailAdviser.Adapters.Ollama;

/// <summary>
/// Stable marker for the Ollama adapter assembly.
/// </summary>
public static class OllamaAssemblyReference
{
    public static Type ApplicationAssemblyMarker => typeof(ApplicationAssemblyReference);

    public static Type DomainAssemblyMarker => typeof(DomainAssemblyReference);
}

