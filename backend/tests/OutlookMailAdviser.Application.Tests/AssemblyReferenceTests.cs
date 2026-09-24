using OutlookMailAdviser.Application;
using OutlookMailAdviser.Domain;

namespace OutlookMailAdviser.Application.Tests;

public sealed class AssemblyReferenceTests
{
    [Fact]
    public void ApplicationAssemblyReferencePointsInwardToDomain()
    {
        Assert.Equal(
            typeof(DomainAssemblyReference),
            ApplicationAssemblyReference.DomainAssemblyMarker);
    }
}
