using FleetOps.Api.Observability;
using FleetOps.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetOps.Api;

public sealed class TrackingHub(
    ICurrentTenantAccessor currentTenantAccessor,
    TrackingConnectionCounter connectionCounter) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(Context.User!);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"organization:{tenant.OrganizationId}");
        connectionCounter.OnConnected();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connectionCounter.OnDisconnected();
        await base.OnDisconnectedAsync(exception);
    }
}
