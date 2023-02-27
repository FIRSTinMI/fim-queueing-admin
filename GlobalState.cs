namespace fim_queueing_admin;

public class GlobalState
{
    public string CurrentSeason { get; }
    public string VersionInfo { get; }
    public GlobalState(string currentSeason, string versionInfo)
    {
        CurrentSeason = currentSeason;
        VersionInfo = versionInfo;
    }
}