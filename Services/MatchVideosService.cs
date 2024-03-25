using fim_queueing_admin.Clients;

namespace fim_queueing_admin.Services;

public class MatchVideosService(FrcEventsClient frcClient) : IService
{
    public async Task<Dictionary<string, MatchVideosModel?>> GetVideosForEvents(string season, IEnumerable<string> events)
    {
        var models = await Task.WhenAll(events.Select(x => GetVideosForEvent(season, x)));
        return new Dictionary<string, MatchVideosModel?>(models);
    }

    private async Task<(int videos, int playedMatches)> GetMatchStatsForLevel(string season, string evt, TournamentLevel level)
    {
        var playedMatches =
            (await frcClient.GetMatchResults(season, evt, level)).Where(m => m.PostResultTime is not null).ToList();

        if (playedMatches.Count == 0) return (0, 0);

        return (playedMatches.Count(m => !string.IsNullOrEmpty(m.MatchVideoLink)), playedMatches.Count);
    }

    public async Task<KeyValuePair<string, MatchVideosModel?>> GetVideosForEvent(string season, string evt)
    {
        try
        {
            var outModel = new MatchVideosModel();
            await Task.WhenAll(new List<Task> {
                Task.Run(async () =>
                {
                    var qualStats = await GetMatchStatsForLevel(season, evt, TournamentLevel.Qual);
                    outModel.QualMatchesTotal = qualStats.playedMatches;
                    outModel.QualVideosAvailable = qualStats.videos;
                }),
                Task.Run(async () =>
                {
                    var playoffStats = await GetMatchStatsForLevel(season, evt, TournamentLevel.Playoff);
                    outModel.PlayoffMatchesTotal = playoffStats.playedMatches;
                    outModel.PlayoffVideosAvailable = playoffStats.videos;
                })
            });

            return new KeyValuePair<string, MatchVideosModel?>(evt, outModel);
        }
        catch (Exception)
        {
            return new KeyValuePair<string, MatchVideosModel?>(evt, null);
        }
    }

    public class MatchVideosModel
    {
        public int? QualVideosAvailable { get; set; }
        public int? QualMatchesTotal { get; set; }
        public int? PlayoffVideosAvailable { get; set; }
        public int? PlayoffMatchesTotal { get; set; }
    }

    public class MatchResponse(IEnumerable<MatchResponse.Match>? matches)
    {
        public IEnumerable<Match>? Matches { get;} = matches;

        public record Match
        {
            public string? MatchVideoLink;
            public string? Description;
            public DateTime? ActualStartTime;
            public DateTime? PostResultTime;
        }
    }
}
