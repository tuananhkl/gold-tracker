namespace GoldTracker.Infrastructure.Config;

public sealed class ScheduleOptions
{
  public const string SectionName = "Schedule";
  public string CronExpression { get; init; } = "*/10 * * * *"; // every 10 minutes
  public TimeOnly? StartHourUtc { get; init; }
  public TimeOnly? EndHourUtc { get; init; }
  public int IntervalMinutes { get; init; } = 10;
}
