using System.Text.Json;
using fim_queueing_admin.Auth;
using fim_queueing_admin.Services;
using Firebase.Database;
using Microsoft.AspNetCore.Mvc;
using Action = fim_queueing_admin.Auth.Action;

namespace fim_queueing_admin.Controllers;

[AuthorizeOperation(Action.Admin)]
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
    public IActionResult FindMissingVideos()
    {
        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> FindMissingVideos([FromForm] FindMissingVideosModel model,
        [FromServices] FindMissingVideosService service)
    {
        var result = await service.FindMissingVideos(model);
        return View("FindMissingVideosResult", result);
    }
    
    [HttpGet]
    public IActionResult SetTwitchListeners()
    {
        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> SetTwitchListeners([FromForm] SetTwitchListenersModel model,
        [FromServices] TwitchListenerService service)
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

    // TODO(@evanlihou): Clean up the following two endpoints, or remove
    [HttpGet]
    public async Task<IActionResult> GetVideoStats([FromServices] MatchVideosService vidSvc, [FromServices] IHttpClientFactory hcf, [FromServices] GlobalState state)
    {
        var events = await hcf.CreateClient("FRC").GetFromJsonAsync<JsonDocument>($"{state.CurrentSeason}/events?districtCode=FIM");
        var filteredEvents = events!.RootElement.GetProperty("Events").EnumerateArray().Where(e => e.GetProperty("dateEnd").GetDateTime() < DateTime.Now.AddDays(2));

        var rawStats = await vidSvc.GetVideosForEvents(state.CurrentSeason, filteredEvents.Select(e => e.GetProperty("code").GetString()!));
        var stats = rawStats.Where(kvp => kvp.Value.QualVideosAvailable > 0 || kvp.Value.PlayoffVideosAvailable > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return Ok("EVENT\tQUAL\tPLAYOFF\n"+string.Join('\n',
            stats.Select(s =>
                    $"{s.Key}\t{decimal.Divide(s.Value.QualVideosAvailable!.Value, s.Value.QualMatchesTotal!.Value) * 100:N2}%\t{decimal.Divide(s.Value.PlayoffVideosAvailable!.Value, s.Value.PlayoffVideosTotal!.Value) * 100:N2}%")
                .ToList()));
    }
    
    [HttpGet]
    public async Task<IActionResult> GetNonMiVideoStats([FromServices] MatchVideosService vidSvc, [FromServices] FirebaseClient client, [FromServices] GlobalState state, [FromServices] IHttpClientFactory hcf)
    {
        var events = await hcf.CreateClient("FRC").GetFromJsonAsync<JsonDocument>($"{state.CurrentSeason}/events");
        var filteredEvents = events!.RootElement.GetProperty("Events").EnumerateArray().Where(e => e.GetProperty("dateEnd").GetDateTime() < DateTime.Now.AddDays(2) && e.GetProperty("districtCode").GetString() != "FIM");

        var rawStats = await vidSvc.GetVideosForEvents(state.CurrentSeason, filteredEvents.Select(e => e.GetProperty("code").GetString()!));
        var stats = rawStats.Where(kvp => kvp.Value.PlayoffVideosAvailable > 0 || kvp.Value.PlayoffVideosAvailable > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return Ok("EVENT\tQUAL\tPLAYOFF\n"+string.Join('\n',
            stats.Select(s =>
                    $"{s.Key}\t{decimal.Divide(s.Value.QualVideosAvailable!.Value, s.Value.QualMatchesTotal!.Value) * 100:N2}%\t{decimal.Divide(s.Value.PlayoffVideosAvailable!.Value, s.Value.PlayoffVideosTotal!.Value) * 100:N2}%")
                .ToList()));
    }

    public class SetTwitchListenersModel
    {
        public string? Usernames { get; set; }
    }
}
