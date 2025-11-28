using System.Globalization;
using Cronos;
using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Infrastructure.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldTracker.Infrastructure.Scheduling;

public sealed class DailyBriefService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly AlertsOptions _options;
  private readonly ILogger<DailyBriefService> _logger;
  private readonly TimeZoneInfo _timeZone;

  public DailyBriefService(
    IServiceProvider serviceProvider,
    IOptions<AlertsOptions> options,
    ILogger<DailyBriefService> logger)
  {
    _serviceProvider = serviceProvider;
    _options = options.Value;
    _logger = logger;
    _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.Timezone);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!_options.Enabled)
    {
      _logger.LogInformation("Daily brief is disabled");
      return;
    }

    var cronExpression = CronExpression.Parse(_options.DailyBriefSchedule);
    _logger.LogInformation("DailyBriefService started. Schedule: {Schedule} ({TZ})", 
      _options.DailyBriefSchedule, _timeZone.Id);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var utcNow = DateTimeOffset.UtcNow;
        var nextUtc = cronExpression.GetNextOccurrence(utcNow, _timeZone, inclusive: false);
        
        if (nextUtc.HasValue)
        {
          var delay = nextUtc.Value - utcNow;
          if (delay > TimeSpan.Zero)
          {
            _logger.LogInformation("Next daily brief at {NextTime} (in {Delay})", 
              TimeZoneInfo.ConvertTime(nextUtc.Value, _timeZone), delay);
            await Task.Delay(delay, stoppingToken);
          }
        }
        else
        {
          await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }

        // Send daily brief
        await SendDailyBriefAsync(stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in DailyBriefService");
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
      }
    }
  }

  public async Task SendDailyBriefAsync(CancellationToken ct = default)
  {
    await using var scope = _serviceProvider.CreateAsyncScope();
    var priceRepo = scope.ServiceProvider.GetRequiredService<IPriceTickRepository>();
    var notifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>();
    var telegramOptions = scope.ServiceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;

    if (!telegramOptions.Enabled || string.IsNullOrEmpty(telegramOptions.DefaultChatId))
    {
      _logger.LogWarning("Telegram not enabled, skipping daily brief");
      return;
    }

    try
    {
      var brands = _options.BrandFilter.Count > 0 ? _options.BrandFilter.ToArray() : new[] { "DOJI", "SJC", "BTMC", "PhucThanh" };
      var message = "📊 *Daily Brief - Giá Vàng 24h*\n\n";

      foreach (var brand in brands)
      {
        var latest = await priceRepo.GetLatestAsync("ring", brand, null, ct);
        if (latest.Count > 0)
        {
          var tick = latest[0];
          var dayOverDay = await priceRepo.GetDayOverDayAsync("ring", brand, null, ct);
          var change = dayOverDay.FirstOrDefault();
          
          var changeText = change != default 
            ? $" ({change.DeltaVsYesterday:+N0;-#,0} VND, {change.Direction})"
            : "";
          
          message += $"*{brand}*\n";
          message += $"Mua: {tick.PriceBuy:N0} VND\n";
          message += $"Bán: {tick.PriceSell:N0} VND{changeText}\n\n";
        }
      }

      // Count successful scrapes (from price_tick inserts in last 24h)
      var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
      // This is simplified - in real implementation, you'd query price_tick.created_at
      message += $"📈 Dữ liệu được cập nhật trong 24h qua\n";
      message += $"⏰ {DateTimeOffset.UtcNow:HH:mm} UTC ({TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone):HH:mm} {_timeZone.Id})";

      var escaped = message
        .Replace("_", "\\_")
        .Replace("*", "\\*")
        .Replace("[", "\\[")
        .Replace("]", "\\]")
        .Replace("(", "\\(")
        .Replace(")", "\\)")
        .Replace("~", "\\~")
        .Replace("`", "\\`")
        .Replace(">", "\\>")
        .Replace("#", "\\#")
        .Replace("+", "\\+")
        .Replace("-", "\\-")
        .Replace("=", "\\=")
        .Replace("|", "\\|")
        .Replace("{", "\\{")
        .Replace("}", "\\}")
        .Replace(".", "\\.")
        .Replace("!", "\\!");

      await notifier.SendTextAsync(telegramOptions.DefaultChatId, escaped, ct);
      _logger.LogInformation("Daily brief sent successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send daily brief");
      throw;
    }
  }
}

