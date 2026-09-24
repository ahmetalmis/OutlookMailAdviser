using OutlookMailAdviser.Domain;

namespace OutlookMailAdviser.Domain.Tests;

public sealed class AssemblyReferenceTests
{
    [Fact]
    public void DomainAssemblyReferenceHasStableAssemblyName()
    {
        var assemblyName = typeof(DomainAssemblyReference).Assembly.GetName().Name;

        Assert.Equal("OutlookMailAdviser.Domain", assemblyName);
    }
}

