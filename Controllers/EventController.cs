using System.Text.Json;
using fim_queueing_admin.Auth;
using fim_queueing_admin.Clients;
using fim_queueing_admin.Services;
using Firebase.Database;
using Microsoft.AspNetCore.Mvc;
using Action = fim_queueing_admin.Auth.Action;

namespace fim_queueing_admin.Controllers;

[AuthorizeOperation(Action.ViewEvent)]
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

    [AuthorizeOperation(Action.ManageEvent)]
    [HttpGet("[action]/{id}")]
    public IActionResult Manage(string id)
    {
        ViewData["id"] = id;
        return View();
    }

    [AuthorizeOperation(Action.ManageEvent)]
    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateState(string id, [FromForm] string eventState)
    {
        // NOTE: This is like really bad. I might even care if it wasn't restricted to admins only.
        await client.Child($"/seasons/{state.CurrentSeason}/events/{id}/state").PutAsync($"\"{eventState}\"");
        return RedirectToAction(nameof(Index));
    }
    
    [AuthorizeOperation(Action.ManageEvent)]
    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateEmbedLink(string id, [FromForm] string link)
    {
        // NOTE: This is like really bad. I might even care if it wasn't restricted to admins only.
        await client.Child($"/seasons/{state.CurrentSeason}/events/{id}/streamEmbedLink").PutAsync($"\"{link}\"");
        return RedirectToAction(nameof(Index));
    }
    
    [AuthorizeOperation(Action.ManageEvent)]
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
    
    [AuthorizeOperation(Action.ManageEvent)]
    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateMessage(string id, [FromForm] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await client.Child($"/seasons/{state.CurrentSeason}/events/{id}/message").DeleteAsync();
        }
        else
        {
            await client.Child($"/seasons/{state.CurrentSeason}/events/{id}/message")
                .PutAsync(JsonSerializer.Serialize(message));
        }
        
        return RedirectToAction(nameof(Index));
    }

    [AuthorizeOperation(Action.ManageEvent)]
    [HttpPost("[action]/{id}")]
    public async Task<IActionResult> UpdateCart(string id, [FromForm] Guid? cartId, [FromServices] AssistantService assistantService)
    {
        var oldCartId = await client.Child($"/seasons/{state.CurrentSeason}/events/{id}/cartId")
            .OnceSingleAsync<Guid?>();
        await client.Child($"/seasons/{state.CurrentSeason}/events/{id}").PatchAsync(JsonSerializer.Serialize(new
        {
            cartId,
        }));

        var notifyTasks = new List<Task>();
        if (oldCartId is not null && oldCartId != cartId)
            notifyTasks.Add(assistantService.SendEventsToCart(oldCartId.Value));
        if (cartId is not null) notifyTasks.Add(assistantService.SendEventsToCart(cartId.Value));

        await Task.WhenAll(notifyTasks);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Get the qual and playoff stats for uploaded videos
    /// </summary>
    /// <param name="districtCode">The district code to search with</param>
    /// <param name="except">When true, show stats for all events NOT in <c>districtCode</c></param>
    /// <returns></returns>
    [AuthorizeOperation(Action.ViewEvent)]
    [HttpGet("VideoStats/{districtCode}")]
    public async Task<IActionResult> GetVideoStats(string districtCode, [FromQuery] bool except, [FromServices] MatchVideosService vidSvc, [FromServices] FrcEventsClient frcApi)
    {
        IEnumerable<FrcEventsApiEventsResponse.ApiEvent> events;
        if (!except)
        {
            events = await frcApi.GetEventInfo(state.CurrentSeason, districtCode: districtCode);
        }
        else
        {
            events = (await frcApi.GetEventInfo(state.CurrentSeason)).Where(e => e.DistrictCode != districtCode);
        }
        
        var filteredEvents = events.Where(e => e.DateEnd < DateTime.Now.AddDays(2));

        var rawStats = await vidSvc.GetVideosForEvents(state.CurrentSeason, filteredEvents.Select(e => e.Code));
        var stats = rawStats.Where(kvp => kvp.Value!.QualVideosAvailable > 0 || kvp.Value.PlayoffVideosAvailable > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return View(stats);
    }
    
    [HttpGet("[action]")]
    public IActionResult FindMissingVideos()
    {
        return View();
    }
    
    [HttpPost("[action]")]
    public async Task<IActionResult> FindMissingVideos([FromForm] FindMissingVideosModel model,
        [FromServices] FindMissingVideosService service)
    {
        var result = await service.FindMissingVideos(model);
        return View("FindMissingVideosResult", result);
    }
}