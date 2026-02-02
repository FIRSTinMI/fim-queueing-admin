using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using fim_queueing_admin.Clients;
using fim_queueing_admin.Models;
using Firebase.Database;

namespace fim_queueing_admin.Services;

public class CreateEventsService : IService
{
    public async Task<CreateEventsResult> CreateEvents(CreateEventsModel model)
    {
        throw new NotImplementedException();
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
    public partial class ApiEvent : IEventResponse
    {
        public IEnumerable<string> Webcasts { get; set; }
        public string DistrictCode { get; set; }
        public string Timezone { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }

        public EventInfo ToEventInfo()
        {
            string? twitchChannel = null;
            if (Webcasts.Count() == 1 && TwitchChannelRegex().IsMatch(Webcasts.First()))
            {
                twitchChannel = TwitchChannelRegex().Match(Webcasts.First()).Groups["name"].Value;
            }

            return new EventInfo
            {
                EventCode = Code,
                Name = Name,
                StartDate = DateStart.AddDays(-1),
                EndDate = DateEnd,
                Timezone = Timezone,
                TwitchChannel = twitchChannel
            };
        }

        [GeneratedRegex(@"^https:\/\/(?:www\.)?twitch\.tv\/(?<name>[\w\d]+)")]
        private static partial Regex TwitchChannelRegex();
    }
}

#pragma warning restore CS8618