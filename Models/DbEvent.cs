// ReSharper disable InconsistentNaming Intentional to match casing of DB model
namespace fim_queueing_admin.Models;

public class DbEvent
{
    public string? name { get; set; }
    public string? eventCode { get; set; }
    public int? field { get; set; }
    public DateTime? start { get; set; }
    public DateTime? end { get; set; }
    public string? streamEmbedLink { get; set; }
    public EventState? state { get; set; }
    public int? currentMatchNumber { get; set; }
    public int? numQualMatches { get; set; }
}