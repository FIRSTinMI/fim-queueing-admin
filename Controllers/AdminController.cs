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
}