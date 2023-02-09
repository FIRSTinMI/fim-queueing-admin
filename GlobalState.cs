namespace fim_queueing_admin;

public class GlobalState
{
    public string CurrentSeason { get; }
    public GlobalState(string currentSeason)
    {
        CurrentSeason = currentSeason;
    }
}