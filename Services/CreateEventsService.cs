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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CreateEventsService> _logger;

    public CreateEventsService(FirebaseClient client, IHttpClientFactory httpClientFactory, ILogger<CreateEventsService> logger)
    {
        _client = client;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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
            string? streamEmbedLink = null;
            if (!string.IsNullOrEmpty(apiEvent.TwitchChannel))
            {
                streamEmbedLink =
                    $"https://player.twitch.tv/?channel={apiEvent.TwitchChannel}&parent=%HOST%&autoplay=true&muted=false";
            }
            
            if (existingEventCodes.Contains(apiEvent.EventCode))
            {
                var evt = dbEvents.First(x => x.Object.eventCode == apiEvent.EventCode)!;
                if (model.UpdateExistingEvents)
                {
                    if (streamEmbedLink != null && string.IsNullOrEmpty(evt.Object.streamEmbedLink))
                    {
                        await _client.Child($"/seasons/{model.Season}/events/{evt.Key}/streamEmbedLink")
                            .PutAsync(JsonSerializer.Serialize(streamEmbedLink, JsonOptions));
                        
                        result.Logs.Add($"Updated stream link for {evt.Object.eventCode}");
                    }

                    if (apiEvent.StartDate != evt.Object.start || apiEvent.EndDate != evt.Object.end)
                    {
                        var tzInfo = TimeZoneInfo.Local;
                        try
                        {
                            tzInfo = TimeZoneInfo.FindSystemTimeZoneById(apiEvent.Timezone);
                        }
                        catch (TimeZoneNotFoundException ex)
                        {
                            _logger.LogWarning(ex, "Could not find time zone {Timezone}", apiEvent.Timezone);
                            result.Errors.Add($"Failed to find time zone for {apiEvent.EventCode}, falling back to local");
                        }
                        
                        var startUtc = (DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(apiEvent.StartDate, tzInfo);
                        var endUtc = (DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(apiEvent.EndDate, tzInfo);
                        await _client.Child($"/seasons/{model.Season}/events/{evt.Key}")
                            .PutAsync(JsonSerializer.Serialize(new
                            {
                                startMs = startUtc.ToUnixTimeMilliseconds(),
                                endMs = endUtc.ToUnixTimeMilliseconds(),
                                start = new DateTimeOffset(apiEvent.StartDate, tzInfo.GetUtcOffset(apiEvent.StartDate)),
                                end = new DateTimeOffset(apiEvent.EndDate, tzInfo.GetUtcOffset(apiEvent.EndDate)),
                            }, JsonOptions));
                        
                        result.Logs.Add($"Updated start/end dates for {evt.Object.eventCode}");
                    }
                }
                else
                {
                    result.Errors.Add($"Skipping duplicate event code {apiEvent.EventCode}");
                }
                continue;
            }

            var timeZoneInfo = TimeZoneInfo.Local;
            try
            {
                timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(apiEvent.Timezone);
            }
            catch (TimeZoneNotFoundException ex)
            {
                _logger.LogWarning(ex, "Could not find time zone {Timezone}", apiEvent.Timezone);
                result.Errors.Add($"Failed to find time zone for {apiEvent.EventCode}, falling back to local");
            }

            string eventKey;
            do
            {
                eventKey = RandomEventKey();
            } while (existingEventKeys.Contains(eventKey));

            var utcStart = (DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(apiEvent.StartDate, timeZoneInfo);
            var utcEnd = (DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(apiEvent.EndDate, timeZoneInfo);
            var dbEvent = new DbEvent()
            {
                eventCode = apiEvent.EventCode,
                startMs = utcStart.ToUnixTimeMilliseconds(),
                endMs = utcEnd.ToUnixTimeMilliseconds(),
                start = new DateTimeOffset(apiEvent.StartDate, timeZoneInfo.GetUtcOffset(apiEvent.StartDate)),
                end = new DateTimeOffset(apiEvent.EndDate, timeZoneInfo.GetUtcOffset(apiEvent.EndDate)),
                state = EventState.Pending,
                name = apiEvent.Name,
                streamEmbedLink = streamEmbedLink,
                dataSource = model.DataSource
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

    private async Task<IEnumerable<EventInfo>> GetEventsFromApi(CreateEventsModel model,
        CreateEventsResult result)
    {
        var httpClient = model.DataSource switch
        {
            "frcEvents" => _httpClientFactory.CreateClient("FRC"),
            "blueAlliance" => _httpClientFactory.CreateClient("TBA"),
            _ => throw new ApplicationException("Unknown data source")
        };
        var eventCodes = model.EventCodes?.Split('\n').Select(x => x.Trim()) ?? Enumerable.Empty<string>();
        var apiEvents = new List<EventInfo>();

        // Get all events for the district
        if (model.DataSource == "frcEvents" && !string.IsNullOrWhiteSpace(model.DistrictCode))
        {
            var districtEvents =
                await httpClient.GetAsync($"{model.Season}/events?districtCode={model.DistrictCode}");
            if (!districtEvents.IsSuccessStatusCode)
            {
                result.Errors.Add("Failed to fetch events for the district");
            }
            else
            {
                await using var contentStream =
                    await districtEvents.Content.ReadAsStreamAsync();
                var deserializedEvents =
                    (await JsonSerializer.DeserializeAsync<FrcEventsApiEventsResponse>(contentStream))?.Events.ToList();
                if (deserializedEvents is null || !deserializedEvents.Any())
                {
                    result.Errors.Add("No events were returned for that district code");
                }
                else
                {
                    apiEvents.AddRange(deserializedEvents.Select(e => e.ToEventInfo()));   
                }
            }
        }
        
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
                    await using var contentStream =
                        await eventResp.Content.ReadAsStreamAsync();
                    var deserializedEvents =
                        (await JsonSerializer.DeserializeAsync<FrcEventsApiEventsResponse>(contentStream))?.Events.ToList();
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
            else if (model.DataSource == "blueAlliance")
            {
                var eventResp = await httpClient.GetAsync($"event/{eventCode}");
                if (!eventResp.IsSuccessStatusCode)
                {
                    result.Errors.Add($"Failed to fetch events for the event code {eventCode}");
                }
                else
                {
                    await using var contentStream =
                        await eventResp.Content.ReadAsStreamAsync();
                    var deserializedEvents = 
                        await JsonSerializer.DeserializeAsync<BlueAllianceApiEventsResponse?>(contentStream);
                    if (deserializedEvents is null)
                    {
                        result.Errors.Add($"No events were returned for event code {eventCode}");
                    }
                    else
                    {
                        apiEvents.Add(deserializedEvents.ToEventInfo());   
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
}

public class CreateEventsModel
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

    /// <summary>
    /// Update the metadata for already existing events
    /// </summary>
    /// <remarks>
    /// Currently supports: <c>streamEmbedLink</c> if null
    /// </remarks>
    public bool UpdateExistingEvents { get; set; } = false;
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

    public List<string> Errors { get; set; } = [];
    public List<string> Logs { get; set; } = [];
    public List<CreatedEvent> CreatedEvents { get; set; } = [];
}

public class EventInfo
{
    public required string EventCode { get; set; }
    public required string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public required string Timezone { get; set; }
    public string? TwitchChannel { get; set; }
}

public interface IEventResponse
{
    public EventInfo ToEventInfo();
}

#pragma warning disable CS8618
public partial class FrcEventsApiEventsResponse
{
    public IEnumerable<ApiEvent> Events { get; set; }
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public partial class ApiEvent : IEventResponse
    {
        public IEnumerable<string> webcasts { get; set; }
        public string timezone { get; set; }
        public string code { get; set; }
        public string name { get; set; }
        public DateTime dateStart { get; set; }
        public DateTime dateEnd { get; set; }

        public EventInfo ToEventInfo()
        {
            string? twitchChannel = null;
            if (webcasts.Count() == 1 && TwitchChannelRegex().IsMatch(webcasts.First()))
            {
                twitchChannel = TwitchChannelRegex().Match(webcasts.First()).Groups["name"].Value;
            }

            return new EventInfo
            {
                EventCode = code,
                Name = name,
                StartDate = dateStart.AddDays(-1),
                EndDate = dateEnd,
                Timezone = timezone,
                TwitchChannel = twitchChannel
            };
        }

        [GeneratedRegex(@"^https:\/\/(?:www\.)?twitch\.tv\/(?<name>[\w\d]+)")]
        private static partial Regex TwitchChannelRegex();
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class BlueAllianceApiEventsResponse : IEventResponse
{
    public class Webcast
    {
        public string channel { get; set; }
        public string type { get; set; }
    }
    public IEnumerable<Webcast> webcasts { get; set; }
    public string timezone { get; set; }
    public string key { get; set; }
    public string name { get; set; }
    public DateTime start_date { get; set; }
    public DateTime end_date { get; set; }

    public EventInfo ToEventInfo()
    {
        string? twitchChannel = null;
        if (webcasts.Count() == 1 && webcasts.First().type == "twitch")
        {
            twitchChannel = webcasts.First().channel;
        }

        return new EventInfo
        {
            EventCode = key,
            Name = name,
            StartDate = start_date.Date.AddDays(-1),
            EndDate = end_date.Date.AddDays(1).AddSeconds(-1),
            Timezone = timezone,
            TwitchChannel = twitchChannel
        };
    }
}
#pragma warning restore CS8618