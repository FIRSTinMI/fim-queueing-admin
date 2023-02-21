using Firebase.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fim_queueing_admin.Controllers;

[Authorize]
public class EventController : Controller
{
    private readonly FirebaseClient _client;
    private readonly GlobalState _state;

    public EventController(FirebaseClient client, GlobalState state)
    {
        _client = client;
        _state = state;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Manage(string id)
    {
        ViewData["id"] = id;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateState(string id, [FromForm] string state)
    {
        // NOTE: This is like really bad. I might even care if it wasn't restricted to admins only.
        await _client.Child($"/seasons/{_state.CurrentSeason}/events/{id}/state").PutAsync($"\"{state}\"");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost]
    public async Task<IActionResult> UpdateEmbedLink(string id, [FromForm] string link)
    {
        // NOTE: This is like really bad. I might even care if it wasn't restricted to admins only.
        await _client.Child($"/seasons/{_state.CurrentSeason}/events/{id}/streamEmbedLink").PutAsync($"\"{link}\"");
        return RedirectToAction(nameof(Index));
    }
}