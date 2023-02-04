using Microsoft.AspNetCore.SignalR;

namespace fim_queueing_admin.Hubs;

public class DisplayHubManager
{
    private readonly ILogger<DisplayHubManager> _logger;
    private readonly Dictionary<string, DisplayInfo> _connectedDisplays = new();

    public DisplayHubManager(ILogger<DisplayHubManager> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, DisplayInfo> GetConnections()
    {
        return _connectedDisplays;
    }

    public void AddConnection(string connectionId, DisplayInfo info)
    {
        if (_connectedDisplays.ContainsKey(connectionId))
        {
            _logger.LogError("Tried to add a connection ID {} that was already being tracked", connectionId);
            return;
        }
        
        _connectedDisplays.Add(connectionId, info);
    }

    public void RemoveConnection(string connectionId)
    {
        if (_connectedDisplays.ContainsKey(connectionId))
        {
            _connectedDisplays.Remove(connectionId);
        }
    }

    public void UpdateInfo(string connectionId, DisplayInfo info)
    {
        _logger.LogInformation("Updating info... {}", info);
        if (!_connectedDisplays.ContainsKey(connectionId))
        {
            _logger.LogError("Tried to add update connection ID {} that isn't tracked", connectionId);
            return;
        }

        info.StartTime = _connectedDisplays[connectionId].StartTime;
        _connectedDisplays[connectionId] = info;
    }
    
    public void UpdateRoute(string connectionId, string route)
    {
        if (!_connectedDisplays.ContainsKey(connectionId))
        {
            _logger.LogError("Tried to add update route on connection ID {} that isn't tracked", connectionId);
            return;
        }

        _connectedDisplays[connectionId].Route = route;
    }
}

public class DisplayInfo
{
    public string? EventKey { get; set; }
    public string? EventCode { get; set; }
    public string? Route { get; set; }
    public string? InstallationId { get; set; }
    public DateTime? StartTime { get; set; } = DateTime.UtcNow;
}

public class DisplayHub : Hub
{
    private readonly DisplayHubManager _manager;

    public DisplayHub(DisplayHubManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Called by the client when it wants to update its info
    /// </summary>
    /// <param name="info"></param>
    public void UpdateInfo(DisplayInfo info)
    {
        _manager.UpdateInfo(Context.ConnectionId, info);
    }
    
    /// <summary>
    /// Called by the client when it navigates to a new route
    /// </summary>
    /// <param name="route"></param>
    public void UpdateRoute(string route)
    {
        _manager.UpdateRoute(Context.ConnectionId, route);
    }

    public override Task OnConnectedAsync()
    {
        _manager.AddConnection(Context.ConnectionId, new DisplayInfo());
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _manager.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}