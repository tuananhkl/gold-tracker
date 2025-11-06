namespace GoldTracker.Infrastructure.Config;

public sealed class ScheduleOptions
{
  public const string SectionName = "Schedule";
  public string FrequencyCron { get; init; } = "*/10 7-21 * * *"; // every 10 min 07:00-21:59 (placeholder)
}
