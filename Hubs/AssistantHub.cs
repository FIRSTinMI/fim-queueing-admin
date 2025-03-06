using System.Text.Json;
using fim_queueing_admin.Auth;
using fim_queueing_admin.Data;
using fim_queueing_admin.Models;
using fim_queueing_admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Hubs;

[Authorize(AuthTokenScheme.AuthenticationScheme)]
public class AssistantHub(FimDbContext dbContext, FimRepository repository, AssistantService assistantService, ILogger<AssistantHub> logger) : Hub
{
    private Guid CartId => Guid.Parse(Context.User?.FindFirst(ClaimTypes.CartId)?.Value ??
                                      throw new ApplicationException("No cart id"));
    
    public override async Task OnConnectedAsync()
    {
        await repository.SetCartLastSeen(CartId, DateTime.MaxValue);
        await SendPendingAlertsToCaller();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await repository.SetCartLastSeen(CartId, DateTime.UtcNow);
    }

    /// <summary>
    /// A message sent when AV Assistant first connects to the Hub. Provides basic info like version number.
    /// </summary>
    /// <param name="info"></param>
    public async Task AppInfo(AssistantInfo info)
    {
        var cart = await GetCart();
        logger.LogInformation(
            "AV Assistant on cart {CartName} (hostname {Hostname}) has connected with version {Version}",
            cart?.Name ?? "<<null>>", info.Hostname, info.Version);

        if (cart is null) return;
        cart.Configuration ??= new Cart.AvCartConfiguration();

        cart.Configuration.AssistantVersion = info.Version;
        await dbContext.SaveChangesAsync();
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

    public async Task GetEvents()
    {
        await assistantService.SendEventsToCart(CartId);
    }

    public async Task GetStreamInfo()
    {
        var cart = await GetCart(true);
        if (cart is null)
        {
            await Clients.Caller.SendAsync("StreamInfo", null);
            return;
        }

        await assistantService.PushStreamKeys(cart, Clients.Caller);
    }
    
    public async Task WriteLog(string message, LogData? data = null)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 500)
        {
            logger.LogError("Got a bad log message from cart {cart}. Either empty or too long.", CartId);
        }

        var log = new EquipmentLog
        {
            EquipmentId = CartId,
            LogMessage = message,
            Category = data?.Category,
            Severity = data?.Severity ?? LogSeverity.Info,
            ExtraInfo = data?.ExtraInfo
        };

        await dbContext.EquipmentLogs.AddAsync(log);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Cart?> GetCart(bool includeStreamInfo = false)
    {
        var query = dbContext.Carts.AsQueryable();
        return await query.FirstOrDefaultAsync(c => c.Id == CartId);
    }

    private async Task SendPendingAlertsToCaller()
    {
        var pendingAlerts = await dbContext.AlertCarts.Where(ac => ac.CartId == CartId && ac.ReadTime == null)
            .Select(ac => ac.Alert).ToListAsync();

        await Clients.Caller.SendAsync("PendingAlerts", pendingAlerts);
    }

    public class AssistantInfo
    {
        public required string Version { get; set; }
        public required string Hostname { get; set; }
    }

    public class LogData
    {
        public string? Category { get; set; }
        public LogSeverity Severity { get; set; } = LogSeverity.Info;
        public JsonElement? ExtraInfo { get; set; }
    }
}