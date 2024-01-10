using fim_queueing_admin.Auth;
using fim_queueing_admin.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Hubs;

[Authorize(AuthTokenScheme.AuthenticationScheme)]
public class AssistantHub(FimDbContext dbContext) : Hub
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
        if (alertCart is null) throw new ApplicationException($"Unable to find alert {alertId} for card {CartId}");
        
        alertCart.ReadTime = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await SendPendingAlertsToCaller();
    }

    private async Task SendPendingAlertsToCaller()
    {
        var pendingAlerts = await dbContext.AlertCarts.Where(ac => ac.CartId == CartId && ac.ReadTime == null)
            .Select(ac => ac.Alert).ToListAsync();

        await Clients.Caller.SendAsync("PendingAlerts", pendingAlerts);
    }
    
    public async Task SendPendingAlertsToEveryone()
    {
        var pendingAlerts = await dbContext.AlertCarts.Where(ac => ac.ReadTime == null)
            .GroupBy(k => k.CartId, v => v.Alert).ToListAsync();

        foreach (var group in pendingAlerts)
        {
            await Clients.User(group.Key.ToString()).SendAsync("PendingAlerts", group.ToList());
        }
    }
}

public static class AssistantHubExtensions
{
    public static async Task SendPendingAlertsToEveryone(this IHubContext<AssistantHub> hubContext, FimDbContext dbContext)
    {
        var pendingAlerts = await dbContext.AlertCarts.Where(ac => ac.ReadTime == null)
            .GroupBy(k => k.CartId, v => v.Alert).ToListAsync();

        foreach (var group in pendingAlerts)
        {
            await hubContext.Clients.User(group.Key.ToString()).SendAsync("PendingAlerts", group.ToList());
        }
    }
}