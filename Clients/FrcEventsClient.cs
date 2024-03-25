using System.Text.Json;
using System.Web;
using fim_queueing_admin.Services;

namespace fim_queueing_admin.Clients;

public class FrcEventsClient(IHttpClientFactory hcf) : IService
{
    private readonly HttpClient _httpClient = hcf.CreateClient("FRC");

    public static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<IEnumerable<FrcEventsApiEventsResponse.ApiEvent>> GetEventInfo(string season, string? eventCode = null,
        string? districtCode = null, int? weekNumber = null)
    {
        var query = QueryString.Empty;
        if (eventCode is not null) query = query.Add("eventCode", eventCode);
        if (districtCode is not null) query = query.Add("districtCode", districtCode);
        if (weekNumber is not null) query = query.Add("weekNumber", weekNumber.Value.ToString());

        var resp = await _httpClient.GetFromJsonAsync<JsonDocument>(
            $"{HttpUtility.UrlEncode(season)}/events{query.ToUriComponent()}");

        if (resp is null) throw new HttpRequestException("FRC Events API did not return any events");

        var events = resp.RootElement.GetProperty("Events").Deserialize<IEnumerable<FrcEventsApiEventsResponse.ApiEvent>>(DefaultJsonOptions);

        if (events is null) throw new ApplicationException("Unable to deserialize events from FRC Events API");

        return events;
    }

    public async Task<IEnumerable<MatchResultInfo>> GetMatchResults(string season, string eventCode,
        TournamentLevel? tournamentLevel = null, int? matchNumber = null, int? start = null, int? end = null)
    {
        var query = QueryString.Empty;
        if (tournamentLevel is not null) query = query.Add("tournamentLevel", tournamentLevel.Value.ToFrcEventsLevel());
        if (matchNumber is not null) query = query.Add("matchNumber", matchNumber.Value.ToString());
        if (start is not null) query = query.Add("start", start.Value.ToString());
        if (end is not null) query = query.Add("end", end.Value.ToString());

        var resp = await _httpClient.GetFromJsonAsync<JsonDocument>(
            $"{HttpUtility.UrlEncode(season)}/matches/{HttpUtility.UrlEncode(eventCode)}{query.ToUriComponent()}");

        if (resp is null) throw new HttpRequestException("FRC Events API did not return any matches");

        var matches = resp.RootElement.GetProperty("Matches").Deserialize<IEnumerable<MatchResultInfo>>(DefaultJsonOptions);

        if (matches is null) throw new ApplicationException("Unable to deserialize matches from FRC Events API");

        return matches;
    }

    public class EventInfo
    {
        public required string Code { get; set; }
        public required string DistrictCode { get; set; }
        public required string Name { get; set; }
        public required DateTime DateStart { get; set; }
        public required DateTime DateEnd { get; set; }
        public required string Timezone { get; set; }
    }

    public class MatchResultInfo
    {
        public required int MatchNumber { get; set; }
        public required string Description { get; set; }
        public string? MatchVideoLink { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? PostResultTime { get; set; }
    }
}