using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fim_queueing_admin.Controllers;

[Authorize]
public class EventController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}