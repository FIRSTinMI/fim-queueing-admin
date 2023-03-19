using fim_queueing_admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fim_queueing_admin.Controllers;

[Authorize]
public class AdminController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
    
    [HttpGet]
    public IActionResult CreateEvents()
    {
        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateEvents([FromForm] CreateEventsModel model, [FromServices] CreateEventsService service)
    {
        var result = await service.CreateEvents(model);
        return View("CreateEventsResult", result);
    }
    
    [HttpGet]
    public IActionResult SetTwitchListeners()
    {
        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> SetTwitchListeners([FromForm] SetTwitchListenersModel model, [FromServices] TwitchListenerService service)
    {
        if (string.IsNullOrWhiteSpace(model.Usernames)) throw new ApplicationException("Bad usernames");
        await service.DeleteAllWebhooks();
        foreach (var username in model.Usernames.Split('\n').Select(x => x.Trim())
                     .Where(x => !string.IsNullOrEmpty(x)))
        {
            await service.AddTwitchListener(username);
        }
        return RedirectToAction("Index");
    }

    public class SetTwitchListenersModel
    {
        public string? Usernames { get; set; }
    }
}