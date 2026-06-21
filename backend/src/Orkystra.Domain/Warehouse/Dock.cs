using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse;

public sealed class Dock : Entity<DockId>
{
    public Dock(DockId id, string code)
        : base(id)
    {
        Code = code;
    }

    public string Code { get; }

    public DockStatus Status { get; private set; } = DockStatus.Available;

    public string? ActiveOperationReference { get; private set; }

    internal void Occupy(string operationReference)
    {
        Status = DockStatus.Occupied;
        ActiveOperationReference = operationReference;
    }

    internal void Release()
    {
        Status = DockStatus.Available;
        ActiveOperationReference = null;
    }
}
