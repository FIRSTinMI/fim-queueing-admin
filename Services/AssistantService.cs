using fim_queueing_admin.Data;
using fim_queueing_admin.Hubs;
using fim_queueing_admin.Models;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Services;

/// <summary>
/// Helpers for interfacing with AssistantHub clients
/// </summary>
public class AssistantService(IServiceProvider serviceProvider, IHubContext<AssistantHub> hubContext, ILogger<AssistantService> logger) : IService
{
    public async Task SendPendingAlertsToEveryone()
    {
        var dbContext = serviceProvider.GetRequiredService<FimDbContext>();
        
        var pendingAlerts = await dbContext.AlertCarts.Where(ac => ac.ReadTime == null)
            .GroupBy(k => k.CartId, v => v.Alert).ToListAsync();

        foreach (var group in pendingAlerts)
        {
            await hubContext.Clients.User(group.Key.ToString()).SendAsync("PendingAlerts", group.ToList());
        }
    }

    public async Task SendEventsToCart(Guid cartId)
    {
        var season = serviceProvider.GetRequiredService<GlobalState>().CurrentSeason;
        var rtdb = serviceProvider.GetRequiredService<FirebaseClient>();

        var events = await rtdb.Child($"/seasons/{season}/events").OrderBy("cartId").EqualTo(cartId.ToString())
            .OnceAsync<DbEvent>();

        await hubContext.Clients.User(cartId.ToString()).SendAsync("Events", events.Select(e => new
        {
            eventKey = e.Key,
            e.Object.eventCode,
            e.Object.name,
            e.Object.start,
            e.Object.end,
            state = e.Object.state?.ToString()
        }));
    }

    public async Task StartStreamForCart(Guid cartId, int? streamNumber)
    {
        logger.LogInformation("Starting {stream} for cart {cartId}",
            streamNumber is null ? "all streams" : $"stream {streamNumber}", cartId);
        await hubContext.Clients.User(cartId.ToString()).SendAsync("StartStream", streamNumber);
    }
    
    public async Task StopStreamForCart(Guid cartId, int? streamNumber)
    {
        logger.LogInformation("Stopping {stream} for cart {cartId}",
            streamNumber is null ? "all streams" : $"stream {streamNumber}", cartId);
        await hubContext.Clients.User(cartId.ToString()).SendAsync("StopStream", streamNumber);
    }
}