using System.Globalization;
using Cronos;
using GoldTracker.Infrastructure.Config;
using GoldTracker.Infrastructure.Scrapers.Btmc;
using GoldTracker.Infrastructure.Scrapers.Doji;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace GoldTracker.Infrastructure.Scheduling;

public sealed class TenMinuteScrapeService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ScheduleOptions _scheduleOptions;
  private readonly ILogger<TenMinuteScrapeService> _logger;
  private readonly TimeZoneInfo _timeZone;

  public TenMinuteScrapeService(
    IServiceProvider serviceProvider,
    IOptions<ScheduleOptions> scheduleOptions,
    ILogger<TenMinuteScrapeService> logger)
  {
    _serviceProvider = serviceProvider;
    _scheduleOptions = scheduleOptions.Value;
    _logger = logger;
    _timeZone = TimeZoneInfo.FindSystemTimeZoneById(
      Environment.GetEnvironmentVariable("TZ") ?? "Asia/Ho_Chi_Minh");
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var cronExpression = CronExpression.Parse("*/10 * * * *"); // Every 10 minutes
    var windowStart = TimeWindow.ParseTime(
      Environment.GetEnvironmentVariable("SCHEDULE_WINDOW_START") ?? _scheduleOptions.StartHourUtc?.ToString("HH:mm") ?? "07:30",
      new TimeOnly(7, 30));
    var windowEnd = TimeWindow.ParseTime(
      Environment.GetEnvironmentVariable("SCHEDULE_WINDOW_END") ?? _scheduleOptions.EndHourUtc?.ToString("HH:mm") ?? "21:00",
      new TimeOnly(21, 0));

    _logger.LogInformation("TenMinuteScrapeService started. Window: {Start} - {End} ({TZ})", 
      windowStart, windowEnd, _timeZone.Id);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var utcNow = DateTimeOffset.UtcNow;
        var localTime = TimeZoneInfo.ConvertTime(utcNow, _timeZone);
        var localTimeOnly = TimeOnly.FromDateTime(localTime.DateTime);

        if (TimeWindow.IsInWindow(localTimeOnly, windowStart, windowEnd))
        {
          _logger.LogInformation("Running scheduled scrapers at {LocalTime}", localTime);
          using var scope = _serviceProvider.CreateScope();
          var dojiScraper = scope.ServiceProvider.GetService<IDojiScraper>();
          if (dojiScraper is not null)
          {
            await dojiScraper.RunOnceAsync(stoppingToken);
          }

          var btmcScraper = scope.ServiceProvider.GetService<IBtmcScraper>();
          if (btmcScraper is not null)
          {
            await btmcScraper.RunOnceAsync(stoppingToken);
          }
        }
        else
        {
          _logger.LogDebug("Outside schedule window ({LocalTime}), skipping scrape", localTime);
        }

        // Calculate next run time
        var nextUtc = cronExpression.GetNextOccurrence(utcNow, _timeZone, inclusive: false);
        if (nextUtc.HasValue)
        {
          var delay = nextUtc.Value - utcNow;
          if (delay > TimeSpan.Zero)
            await Task.Delay(delay, stoppingToken);
        }
        else
        {
          await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in TenMinuteScrapeService");
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
      }
    }
  }
}

