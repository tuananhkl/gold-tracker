using GoldTracker.Application.Contracts.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoldTracker.Infrastructure.Scheduling;

public sealed class DailySnapshotService : BackgroundService
{
  private readonly IDailySnapshotRepository _snapshotRepo;
  private readonly ILogger<DailySnapshotService> _logger;
  private readonly TimeZoneInfo _timeZone;

  public DailySnapshotService(
    IDailySnapshotRepository snapshotRepo,
    ILogger<DailySnapshotService> logger)
  {
    _snapshotRepo = snapshotRepo;
    _logger = logger;
    _timeZone = TimeZoneInfo.FindSystemTimeZoneById(
      Environment.GetEnvironmentVariable("TZ") ?? "Asia/Ho_Chi_Minh");
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var snapshotTimeStr = Environment.GetEnvironmentVariable("SNAPSHOT_AT") ?? "21:05";
    var snapshotTime = TimeWindow.ParseTime(snapshotTimeStr, new TimeOnly(21, 5));

    _logger.LogInformation("DailySnapshotService started. Snapshot time: {Time} ({TZ})", 
      snapshotTime, _timeZone.Id);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var utcNow = DateTimeOffset.UtcNow;
        var localTime = TimeZoneInfo.ConvertTime(utcNow, _timeZone);
        var localTimeOnly = TimeOnly.FromDateTime(localTime.DateTime);
        var localDate = DateOnly.FromDateTime(localTime.DateTime);

        // Check if it's time to run snapshot
        if (localTimeOnly >= snapshotTime && localTimeOnly < snapshotTime.AddMinutes(5))
        {
          _logger.LogInformation("Running daily snapshot for {Date} at {LocalTime}", localDate, localTime);
          await _snapshotRepo.UpsertDailyCloseAsync(localDate, stoppingToken);
          _logger.LogInformation("Daily snapshot completed for {Date}", localDate);

          // Wait until next day to avoid running multiple times
          var nextDay = localTime.Date.AddDays(1).Add(snapshotTime.ToTimeSpan());
          var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextDay, _timeZone);
          var delay = nextUtc - utcNow;
          if (delay > TimeSpan.Zero)
            await Task.Delay(delay, stoppingToken);
          else
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
        else
        {
          // Check again in 1 minute
          await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in DailySnapshotService");
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
      }
    }
  }
}

