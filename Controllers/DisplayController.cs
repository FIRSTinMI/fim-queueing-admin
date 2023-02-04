using fim_queueing_admin.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace fim_queueing_admin.Controllers;

[Authorize]
public class DisplayController : Controller
{
    private readonly IHubContext<DisplayHub> _hubCtx;
    
    public DisplayController(IHubContext<DisplayHub> hubCtx)
    {
        _hubCtx = hubCtx;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Manage(string id)
    {
        ViewBag.id = id;
        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> SendRefresh(string id)
    {
        await _hubCtx.Clients.Client(id).SendAsync("SendRefresh");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost]
    public async Task<IActionResult> SendRefreshToAll()
    {
        await _hubCtx.Clients.All.SendAsync("SendRefresh");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost]
    public async Task<IActionResult> Identify(string id)
    {
        await _hubCtx.Clients.Client(id).SendAsync("Identify");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost]
    public async Task<IActionResult> IdentifyAll()
    {
        await _hubCtx.Clients.All.SendAsync("Identify");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost]
    public async Task<IActionResult> SendNewRoute(string id, [FromForm] string route)
    {
        await _hubCtx.Clients.Client(id).SendAsync("SendNewRoute", route);
        return RedirectToAction(nameof(Index));
    }
}