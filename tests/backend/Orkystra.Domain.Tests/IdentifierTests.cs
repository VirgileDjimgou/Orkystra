using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Tests;

public sealed class IdentifierTests
{
    [Fact]
    public void WarehouseId_CreateFailsForEmptyGuid()
    {
        var result = WarehouseId.Create(Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("domain.identifier.empty", result.Error.Code);
    }

    [Fact]
    public void WarehouseId_CreateSucceedsForNonEmptyGuid()
    {
        var guid = Guid.NewGuid();

        var result = WarehouseId.Create(guid);

        Assert.True(result.IsSuccess);
        Assert.Equal(guid, result.Value.Value);
    }

    [Fact]
    public void RouteId_NewCreatesNonEmptyIdentifier()
    {
        var identifier = RouteId.New();

        Assert.NotEqual(Guid.Empty, identifier.Value);
    }
}
