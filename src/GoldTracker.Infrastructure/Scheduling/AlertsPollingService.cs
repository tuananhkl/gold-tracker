using System.Globalization;
using Cronos;
using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Alerts;
using GoldTracker.Infrastructure.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldTracker.Infrastructure.Scheduling;

public sealed class AlertsPollingService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly AlertsOptions _options;
  private readonly ILogger<AlertsPollingService> _logger;
  private readonly TimeZoneInfo _timeZone;

  public AlertsPollingService(
    IServiceProvider serviceProvider,
    IOptions<AlertsOptions> options,
    ILogger<AlertsPollingService> logger)
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
      _logger.LogInformation("Alerts polling is disabled");
      return;
    }

    _logger.LogInformation("AlertsPollingService started. Polling every 10 minutes");

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IAlertEvaluator>();
        var notifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>();
        var alertRepo = scope.ServiceProvider.GetRequiredService<IAlertEventRepository>();
        var telegramOptions = scope.ServiceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;

        if (!telegramOptions.Enabled || string.IsNullOrEmpty(telegramOptions.DefaultChatId))
        {
          _logger.LogDebug("Telegram not enabled or chat ID not configured, skipping alerts dispatch");
          await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
          continue;
        }

        var allCandidates = new List<AlertCandidate>();
        
        // Evaluate all alert types
        var priceJumpAlerts = await evaluator.EvaluatePriceJumpAsync(_options.PriceJumpPercent, stoppingToken);
        allCandidates.AddRange(priceJumpAlerts);

        var noDataAlerts = await evaluator.EvaluateNoDataAsync(_options.NoDataMinutes, stoppingToken);
        allCandidates.AddRange(noDataAlerts);

        var scraperErrorAlerts = await evaluator.EvaluateScraperErrorsAsync(stoppingToken);
        allCandidates.AddRange(scraperErrorAlerts);

        // Deduplicate and send
        foreach (var candidate in allCandidates)
        {
          try
          {
            // Check if already sent recently (30 min window)
            if (!string.IsNullOrEmpty(candidate.DedupKey))
            {
              var existing = await alertRepo.GetLastByDedupKeyAsync(candidate.DedupKey, TimeSpan.FromMinutes(30), stoppingToken);
              if (existing != null && existing.Severity == candidate.Severity)
              {
                _logger.LogDebug("Skipping duplicate alert: {DedupKey}", candidate.DedupKey);
                continue;
              }
            }

            // Send to Telegram
            var message = FormatAlertMessage(candidate);
            await notifier.SendTextAsync(telegramOptions.DefaultChatId, message, stoppingToken);

            // Save to DB
            var alertEvent = new AlertEvent
            {
                Kind = candidate.Kind,
                Brand = candidate.Brand,
                Region = candidate.Region,
                ProductKind = candidate.ProductKind,
                Message = candidate.Message,
                Severity = candidate.Severity,
                Meta = candidate.Meta,
                DedupKey = candidate.DedupKey
            };
            await alertRepo.AddAsync(alertEvent, stoppingToken);

            _logger.LogInformation(
              "Alert sent: {Kind} {Severity} - {Message}",
              candidate.Kind, candidate.Severity, candidate.Message);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to process alert candidate: {Kind}", candidate.Kind);
          }
        }

        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in AlertsPollingService");
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
      }
    }
  }

  private static string FormatAlertMessage(AlertCandidate candidate)
  {
    var emoji = candidate.Severity switch
    {
      "high" => "🔴",
      "warn" => "⚠️",
      _ => "ℹ️"
    };
    
    var escaped = candidate.Message
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

    return $"{emoji} *Alert: {candidate.Kind}*\n\n{escaped}";
  }
}

