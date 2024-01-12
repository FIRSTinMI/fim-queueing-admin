using System.Text.Json;
using fim_queueing_admin.Services;
using Firebase.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fim_queueing_admin.Controllers;

[Authorize]
[Route("[controller]")]
public class EventController(FirebaseClient client, GlobalState state) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("[action]/{eventCode}")]
    [Produces("application/json")]
    public async Task<ActionResult> GetEventVideoStatus(string eventCode, [FromServices] MatchVideosService vidService)
    {
        if (string.IsNullOrEmpty(eventCode)) return BadRequest();

        ViewData["eventCode"] = eventCode;
        ViewData["season"] = state.CurrentSeason;

        var vidStatus = await vidService.GetVideosForEvent(state.CurrentSeason, eventCode);
        
        return PartialView("_GetEventVideoStatus", vidStatus.Value);
    }

    [HttpGet("[action]/{id}")]
    public IActionResult Manage(string id)
    {
        ViewData["id"] = id;
        return View();
    }

    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateState(string id, [FromForm] string eventState)
    {
        // NOTE: This is like really bad. I might even care if it wasn't restricted to admins only.
        await client.Child($"/seasons/{state.CurrentSeason}/events/{id}/state").PutAsync($"\"{eventState}\"");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateEmbedLink(string id, [FromForm] string link)
    {
        // NOTE: This is like really bad. I might even care if it wasn't restricted to admins only.
        await client.Child($"/seasons/{state.CurrentSeason}/events/{id}/streamEmbedLink").PutAsync($"\"{link}\"");
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateDateTimes(string id, [FromForm] DateTime start, [FromForm] DateTime end, [FromForm] string offset)
    {
        var startDateTimeOffset = new DateTimeOffset(start, TimeSpan.Parse(offset));
        var endDateTimeOffset = new DateTimeOffset(end, TimeSpan.Parse(offset));
        
        await client.Child($"/seasons/{state.CurrentSeason}/events/{id}")
            .PatchAsync(JsonSerializer.Serialize(new
            {
                startMs = startDateTimeOffset.ToUnixTimeMilliseconds(),
                endMs = endDateTimeOffset.ToUnixTimeMilliseconds(),
                start = startDateTimeOffset,
                end = endDateTimeOffset,
            }));
        
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateCart(string id, [FromForm] Guid cartId)
    {
        await client.Child($"/seasons/{state.CurrentSeason}/events/{id}").PatchAsync(JsonSerializer.Serialize(new
        {
            cartId,
        }));

        return RedirectToAction(nameof(Index));
    }
}