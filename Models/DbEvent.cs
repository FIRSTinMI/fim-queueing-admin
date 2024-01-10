// ReSharper disable InconsistentNaming Intentional to match casing of DB model
namespace fim_queueing_admin.Models;

public class DbEvent
{
    public string? name { get; set; }
    public string? eventCode { get; set; }
    public int? field { get; set; }
    public Guid cartId { get; set; }
    public DateTimeOffset? start { get; set; }
    public DateTimeOffset? end { get; set; }
    public long? startMs { get; set; }
    public long? endMs { get; set; }
    public string? streamEmbedLink { get; set; }
    public EventState? state { get; set; }
    public int? currentMatchNumber { get; set; }
    public string? playoffMatchNumber { get; set; }
    public int? numQualMatches { get; set; }
    public string dataSource { get; set; }
}