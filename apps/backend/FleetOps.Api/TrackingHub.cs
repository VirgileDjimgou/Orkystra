using FleetOps.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetOps.Api;

public sealed class TrackingHub(ICurrentTenantAccessor currentTenantAccessor) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(Context.User!);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"organization:{tenant.OrganizationId}");
        await base.OnConnectedAsync();
    }
}
