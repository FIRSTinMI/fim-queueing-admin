namespace fim_queueing_admin;

public enum EventState
{
  Pending,
  AwaitingQualSchedule,
  QualsInProgress,
  AwaitingAlliances,
  PlayoffsInProgress,
  EventOver
}