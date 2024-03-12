using System.Collections.Concurrent;

namespace fim_queueing_admin.Services;

public class FindMissingVideosService(IHttpClientFactory httpClientFactory) : IService
{
    public async Task<FindMissingVideosResult> FindMissingVideos(FindMissingVideosModel model)
    {
        var result = new FindMissingVideosResult();
        if (model.Season is null or <= 0)
        {
            result.Errors.Add("Season is required");
            return result;
        }

        var httpClient = model.DataSource switch
        {
            "frcEvents" => httpClientFactory.CreateClient("FRC"),
            _ => throw new ApplicationException("Unknown data source")
        };

        var apiEvents = await GetEventsFromApi(httpClient, model, result);

        var results = new ConcurrentBag<FindMissingVideosResult.EventResult>();
        await Parallel.ForEachAsync(apiEvents, async (info, _) =>
        {
            var qualTask =
                GetMissingVideosForTournamentLevel(httpClient, model.Season!.Value, info, TournamentLevel.Qual);
            var playoffTask =
                GetMissingVideosForTournamentLevel(httpClient, model.Season!.Value, info, TournamentLevel.Playoff);
            await qualTask;
            await playoffTask;

            qualTask.Result.AddRange(playoffTask.Result);

            results.Add(new FindMissingVideosResult.EventResult
            {
                EventCode = info.EventCode,
                EventName = info.Name,
                MissingVideos = qualTask.Result
            });
        });

        result.Events = results.ToList();

        return result;
    }

    private static async Task<IEnumerable<EventInfo>> GetEventsFromApi(HttpClient httpClient,
        FindMissingVideosModel model, FindMissingVideosResult result)
    {
        var eventCodes = model.EventCodes?.Split('\n').Select(x => x.Trim()) ?? Enumerable.Empty<string>();
        var apiEvents = new List<EventInfo>();

        // Get all the individually requested events
        foreach (var eventCode in eventCodes)
        {
            if (string.IsNullOrWhiteSpace(eventCode)) continue;
            if (apiEvents.Any(x => x.EventCode == eventCode))
            {
                continue;
            }

            if (!eventCode.All(char.IsLetterOrDigit))
            {
                result.Errors.Add($"Bad event code {eventCode}");
                continue;
            }

            if (model.DataSource == "frcEvents")
            {
                var eventResp = await httpClient.GetAsync($"{model.Season}/events?eventCode={eventCode}");
                if (!eventResp.IsSuccessStatusCode)
                {
                    result.Errors.Add($"Failed to fetch events for the event code {eventCode}");
                }
                else
                {
                    var deserializedEvents = (await eventResp.Content.ReadFromJsonAsync<FrcEventsApiEventsResponse>())
                        ?.Events.ToList();
                    if (deserializedEvents is null || deserializedEvents.Count != 1)
                    {
                        result.Errors.Add($"No events were returned for event code {eventCode}");
                    }
                    else
                    {
                        apiEvents.AddRange(deserializedEvents.Select(e => e.ToEventInfo()));
                    }
                }
            }
            else
            {
                throw new ApplicationException("Unknown data source");
            }
        }

        return apiEvents;
    }

    private static async Task<List<FindMissingVideosResult.MissingVideo>> GetMissingVideosForTournamentLevel(
        HttpClient httpClient, int season, EventInfo eventInfo, TournamentLevel tournamentLevel)
    {
        var matches = await httpClient.GetFromJsonAsync<MatchVideosService.MatchResponse>(
            $"{season}/matches/{eventInfo.EventCode}?tournamentLevel={tournamentLevel.ToFrcEventsLevel()}");
        return matches!.Matches!.Where(m => string.IsNullOrEmpty(m.MatchVideoLink)).Select(m =>
            new FindMissingVideosResult.MissingVideo
            {
                MatchName = m.Description,
                StartTimeLocal = m.ActualStartTime
            }).ToList();
    }
}

public enum TournamentLevel
{
    Qual,
    Playoff
}

public static class TournamentLevelExtensions
{
    public static string ToFrcEventsLevel(this TournamentLevel level)
    {
        return level switch
        {
            TournamentLevel.Qual => "Qualification",
            TournamentLevel.Playoff => "Playoff",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }
}

public class FindMissingVideosModel
{
    public string DataSource { get; set; } = "frcEvents";
    public int? Season { get; set; }

    /// <summary>
    /// Add all events in the given district. Only supported with `frcEvents`.
    /// </summary>
    public string? DistrictCode { get; set; }

    /// <summary>
    /// Newline-separated list of event codes.
    /// </summary>
    public string? EventCodes { get; set; }
}

public class FindMissingVideosResult
{
    public class MissingVideo
    {
        public string? MatchName { get; set; }
        public DateTime? StartTimeLocal { get; set; }
    }

    public class EventResult
    {
        public string? EventCode { get; set; }
        public string? EventName { get; set; }
        public List<MissingVideo> MissingVideos { get; set; } = [];
    }

    public List<EventResult> Events { get; set; } = [];

    public List<string> Errors { get; set; } = [];
}