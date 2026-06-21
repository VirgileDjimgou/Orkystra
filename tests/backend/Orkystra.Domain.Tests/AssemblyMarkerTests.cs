using Orkystra.Domain;

namespace Orkystra.Domain.Tests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void DomainAssemblyIsAvailable()
    {
        Assert.Equal("Orkystra.Domain", typeof(AssemblyMarker).Namespace);
    }
}
