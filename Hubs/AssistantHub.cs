using fim_queueing_admin.Auth;
using fim_queueing_admin.Data;
using fim_queueing_admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Hubs;

[Authorize(AuthTokenScheme.AuthenticationScheme)]
public class AssistantHub(FimDbContext dbContext, AssistantService assistantService) : Hub
{
    private Guid CartId => Guid.Parse(Context.User?.FindFirst(ClaimTypes.CartId)?.Value ??
                                      throw new ApplicationException("No cart id"));
    
    public override async Task OnConnectedAsync()
    {
        await dbContext.Carts.Where(c => c.Id == CartId).ExecuteUpdateAsync(c => c
            .SetProperty(p => p.LastSeen, DateTime.MaxValue));
        await SendPendingAlertsToCaller();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await dbContext.Carts.Where(c => c.Id == CartId).ExecuteUpdateAsync(c => c
            .SetProperty(p => p.LastSeen, DateTime.UtcNow));
    }

    public async Task MarkAlertRead(Guid alertId)
    {
        var alertCart =
            await dbContext.AlertCarts.SingleOrDefaultAsync(ac => ac.CartId == CartId && ac.AlertId == alertId);
        if (alertCart is null) throw new ApplicationException($"Unable to find alert {alertId} for cart {CartId}");
        
        alertCart.ReadTime = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await SendPendingAlertsToCaller();
    }

    // ReSharper disable once UnusedMember.Global
    public async Task GetEvents()
    {
        await assistantService.SendEventsToCart(CartId);
    }

    private async Task SendPendingAlertsToCaller()
    {
        var pendingAlerts = await dbContext.AlertCarts.Where(ac => ac.CartId == CartId && ac.ReadTime == null)
            .Select(ac => ac.Alert).ToListAsync();

        await Clients.Caller.SendAsync("PendingAlerts", pendingAlerts);
    }
}