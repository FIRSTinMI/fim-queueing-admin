namespace fim_queueing_admin.Services;

public class MatchVideosService : IService
{
    private readonly HttpClient _frcClient;

    public MatchVideosService(IHttpClientFactory hcf)
    {
        _frcClient = hcf.CreateClient("FRC");
    }
    
    public async Task<Dictionary<string, MatchVideosModel?>> GetVideosForEvents(string season, IEnumerable<string> events)
    {
        var models = await Task.WhenAll(events.Select(x => GetVideosForEvent(season, x)));
        return new Dictionary<string, MatchVideosModel?>(models);
    }

    public async Task<KeyValuePair<string, MatchVideosModel?>> GetVideosForEvent(string season, string evt)
    {
        try
        {
            var outModel = new MatchVideosModel();
            await Task.WhenAll(new List<Task> {
                Task.Run(async () =>
                {
                    var matches = await _frcClient.GetFromJsonAsync<MatchResponse>($"{season}/matches/{evt}?tournamentLevel=qual");
                    outModel.QualMatchesTotal = matches?.Matches?.Count();
                    outModel.QualVideosAvailable = matches?.Matches?.Count(x => !string.IsNullOrEmpty(x.MatchVideoLink));
                }),
                Task.Run(async () =>
                {
                    var matches = await _frcClient.GetFromJsonAsync<MatchResponse>($"{season}/matches/{evt}?tournamentLevel=playoff");
                    outModel.PlayoffVideosTotal = matches?.Matches?.Count();
                    outModel.PlayoffVideosAvailable = matches?.Matches?.Count(x => !string.IsNullOrEmpty(x.MatchVideoLink));
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
        public int? PlayoffVideosTotal { get; set; }
    }

    public class MatchResponse
    {
        public IEnumerable<Match>? Matches { get; set; }
        public class Match
        {
            public string? MatchVideoLink { get; set; }
        }
    }
}
