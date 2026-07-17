using FleetOps.Api.Security;
using Microsoft.AspNetCore.SignalR;

namespace FleetOps.Api.Operations;

public sealed class OperationsHub(ICurrentTenantAccessor currentTenantAccessor) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(Context.User!);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"organization:{tenant.OrganizationId}:operations");
        await base.OnConnectedAsync();
    }
}
