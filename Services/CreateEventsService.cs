using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using fim_queueing_admin.Models;
using Firebase.Database;

namespace fim_queueing_admin.Services;

public class CreateEventsService : IService
{
    private readonly FirebaseClient _client;
    private readonly HttpClient _frcApiClient;
    private readonly ILogger<CreateEventsService> _logger;
    private static readonly Regex TwitchChannelRegex = new Regex(@"$https:\/\/twitch\.tv/([\w\d]+)");

    public CreateEventsService(FirebaseClient client, IHttpClientFactory httpClientFactory, ILogger<CreateEventsService> logger)
    {
        _client = client;
        _logger = logger;
        _frcApiClient = httpClientFactory.CreateClient("FRC");
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly Random Random = new();
    private static string RandomEventKey(int length = 10)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    public async Task<CreateEventsResult> CreateEvents(CreateEventsModel model)
    {
        var result = new CreateEventsResult();
        if (model.Season is null or <= 0)
        {
            result.Errors.Add("Season is required");
            return result;
        }

        var apiEvents = await GetEventsFromApi(model, result);
        var dbEvents = await _client.Child($"/seasons/{model.Season}/events")
            .OnceAsync<DbEvent>();
        var existingEventCodes = dbEvents.Select(x => x.Object.eventCode).ToList();
        var existingEventKeys = dbEvents.Select(x => x.Key).ToList();

        foreach (var apiEvent in apiEvents)
        {
            if (existingEventCodes.Contains(apiEvent.code))
            {
                result.Errors.Add($"Skipping duplicate event code {apiEvent.code}");
                continue;
            }

            var timeZoneInfo = TimeZoneInfo.Local;
            try
            {
                timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(apiEvent.timezone);
            }
            catch (TimeZoneNotFoundException ex)
            {
                _logger.LogWarning(ex, "Could not find time zone {Timezone}", apiEvent.timezone);
                result.Errors.Add($"Failed to find time zone for {apiEvent.code}, falling back to local");
            }

            string? streamEmbedLink = null;
            if (apiEvent.webcasts.Count() == 1 && TwitchChannelRegex.IsMatch(apiEvent.webcasts.First()))
            {
                streamEmbedLink =
                    $"https://player.twitch.tv/?channel={TwitchChannelRegex.Matches(apiEvent.webcasts.First())}&parent=fim-queueing.web.app&autoplay=true&muted=false";
            }

            string eventKey;
            do
            {
                eventKey = RandomEventKey();
            } while (existingEventKeys.Contains(eventKey));

            var utcStart = (DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(apiEvent.dateStart, timeZoneInfo);
            var utcEnd = (DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(apiEvent.dateEnd, timeZoneInfo);
            var dbEvent = new DbEvent()
            {
                eventCode = apiEvent.code,
                startMs = utcStart.ToUnixTimeMilliseconds(),
                endMs = utcEnd.ToUnixTimeMilliseconds(),
                start = new DateTimeOffset(apiEvent.dateStart, timeZoneInfo.GetUtcOffset(apiEvent.dateStart)),
                end = new DateTimeOffset(apiEvent.dateEnd, timeZoneInfo.GetUtcOffset(apiEvent.dateEnd)),
                state = EventState.Pending,
                name = apiEvent.name,
                streamEmbedLink = streamEmbedLink
            };

            try
            {
                await _client.Child($"/seasons/{model.Season}/events/{eventKey}")
                    .PutAsync(JsonSerializer.Serialize(dbEvent, JsonOptions));

                result.CreatedEvents.Add(new CreateEventsResult.CreatedEvent()
                {
                    EventKey = eventKey,
                    EventName = dbEvent.name,
                    EventCode = dbEvent.eventCode,
                    StartDate = dbEvent.start.GetValueOrDefault()
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to create event {dbEvent.eventCode} in the database");
                _logger.LogError(ex, "Failed to create event {Code}", dbEvent.eventCode);
            }
        }

        return result;
    }

    private async Task<IEnumerable<ApiEventsResponse.ApiEvent>> GetEventsFromApi(CreateEventsModel model,
        CreateEventsResult result)
    {
        var eventCodes = model.EventCodes.Split('\n').Select(x => x.Trim());
        var apiEvents = new List<ApiEventsResponse.ApiEvent>();

        // Get all events for the district
        if (!string.IsNullOrWhiteSpace(model.DistrictCode))
        {
            var districtEvents =
                await _frcApiClient.GetAsync($"{model.Season}/events?districtCode={model.DistrictCode}");
            if (!districtEvents.IsSuccessStatusCode)
            {
                result.Errors.Add("Failed to fetch events for the district");
            }
            else
            {
                await using var contentStream =
                    await districtEvents.Content.ReadAsStreamAsync();
                var deserializedEvents =
                    (await JsonSerializer.DeserializeAsync<ApiEventsResponse>(contentStream))?.Events.ToList();
                if (deserializedEvents is null || !deserializedEvents.Any())
                {
                    result.Errors.Add("No events were returned for that district code");
                }
                else
                {
                    apiEvents.AddRange(deserializedEvents);   
                }
            }
        }
        
        // Get all the individually requested events
        foreach (var eventCode in eventCodes)
        {
            if (string.IsNullOrWhiteSpace(eventCode)) continue;
            if (apiEvents.Any(x => x.code == eventCode))
            {
                continue;
            }

            if (!eventCode.All(char.IsLetterOrDigit))
            {
                result.Errors.Add($"Bad event code {eventCode}");
                continue;
            }
            var apiEvent = await _frcApiClient.GetAsync($"{model.Season}/events?eventCode={eventCode}");
            if (!apiEvent.IsSuccessStatusCode)
            {
                result.Errors.Add($"Failed to fetch events for event code {eventCode}");
                continue;
            }
            await using var contentStream =
                await apiEvent.Content.ReadAsStreamAsync();
            var deserializedEvents =
                (await JsonSerializer.DeserializeAsync<ApiEventsResponse>(contentStream))?.Events.ToList();
            if (deserializedEvents is null || deserializedEvents.Count != 1)
            {
                result.Errors.Add($"No events were returned for event code {eventCode}");
                continue;
            }
            apiEvents.AddRange(deserializedEvents);
            
        }

        return apiEvents;
    }
}

public class CreateEventsModel
{
    public int? Season { get; set; }
    /// <summary>
    /// Add all events in the given district
    /// </summary>
    public string? DistrictCode { get; set; }

    /// <summary>
    /// Newline-separated list of FRC event codes. TBA support coming later.
    /// </summary>
    public string EventCodes { get; set; } = "";
}

public class CreateEventsResult
{
    public class CreatedEvent
    {
        public string? EventKey { get; set; }
        public string? EventCode { get; set; }
        public string? EventName { get; set; }
        public DateTimeOffset StartDate { get; set; }
    }

    public List<string> Errors { get; set; } = new();
    public List<CreatedEvent> CreatedEvents { get; set; } = new();
}

#pragma warning disable CS8618
public class ApiEventsResponse
{
    public IEnumerable<ApiEvent> Events { get; set; }
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ApiEvent
    {
        public IEnumerable<string> webcasts { get; set; }
        public string timezone { get; set; }
        public string code { get; set; }
        public string name { get; set; }
        public DateTime dateStart { get; set; }
        public DateTime dateEnd { get; set; }
    }
}
#pragma warning restore CS8618