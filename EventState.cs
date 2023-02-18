using System.ComponentModel.DataAnnotations;

namespace fim_queueing_admin;

public enum EventState
{
  [Display(Name = "Pending")]
  Pending,
  [Display(Name = "Awaiting Qual Schedule")]
  AwaitingQualSchedule,
  [Display(Name = "Quals in Progress")]
  QualsInProgress,
  [Display(Name = "Awaiting Alliances")]
  AwaitingAlliances,
  [Display(Name = "Playoffs in Progress")]
  PlayoffsInProgress,
  [Display(Name = "Event Over")]
  EventOver
}