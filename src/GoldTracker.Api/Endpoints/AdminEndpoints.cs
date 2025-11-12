using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Infrastructure.Scrapers.Btmc;
using GoldTracker.Infrastructure.Scrapers.Doji;
using GoldTracker.Infrastructure.Scrapers.Sjc;
using GoldTracker.Infrastructure.Scrapers.PhucThanh;
using GoldTracker.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GoldTracker.Api.Endpoints;

public static class AdminEndpoints
{
  public static void MapAdminEndpoints(this WebApplication app)
  {
    var group = app.MapGroup("/admin").WithTags("Admin");

    // POST /admin/scrape/doji?mode=once
    group.MapPost("/scrape/doji", async (string? mode, IDojiScraper scraper, CancellationToken ct) =>
    {
      if (mode != "once")
        return Results.BadRequest(new { error = "mode must be 'once'" });

      var inserted = await scraper.RunOnceAsync(ct);
      return Results.Json(new { inserted });
    }).WithTags("Admin");

    group.MapPost("/scrape/sjc", async (string? mode, ISjcScraper scraper, CancellationToken ct) =>
    {
      if (mode != "once")
        return Results.BadRequest(new { error = "mode must be 'once'" });

      var inserted = await scraper.RunOnceAsync(ct);
      return Results.Json(new { inserted });
    }).WithTags("Admin");

    group.MapGet("/scrape/sjc/health", (ISjcScraper scraper) =>
    {
      var h = scraper.GetHealth();
      return Results.Json(new
      {
        h.LastSuccess,
        h.LastFailure,
        h.LastError,
        h.ConsecutiveFailures,
        h.LastInserted,
        h.TotalInserted,
        h.TotalRuns,
        h.LastAnomalyCount,
        h.LastAnomalySummary
      });
    }).WithTags("Admin");

    // PhucThanh
    group.MapPost("/scrape/phucthanh", async (string? mode, IPhucThanhScraper scraper, CancellationToken ct) =>
    {
      if (mode != "once")
        return Results.BadRequest(new { error = "mode must be 'once'" });

      var inserted = await scraper.RunOnceAsync(ct);
      return Results.Json(new { inserted });
    }).WithTags("Admin");

    group.MapGet("/scrape/phucthanh/health", (IPhucThanhScraper scraper) =>
    {
      var health = scraper.GetHealth();
      return Results.Json(new
      {
        health.LastSuccess,
        health.LastFailure,
        health.LastError,
        health.ConsecutiveFailures,
        health.LastInserted,
        health.TotalInserted,
        health.TotalRuns,
        health.LastAnomalyCount,
        health.LastAnomalySummary
      });
    }).WithTags("Admin");

    group.MapPost("/scrape/btmc", async (string? mode, IBtmcScraper scraper, CancellationToken ct) =>
    {
      if (mode != "once")
        return Results.BadRequest(new { error = "mode must be 'once'" });

      var inserted = await scraper.RunOnceAsync(ct);
      return Results.Json(new { inserted });
    }).WithTags("Admin");

    group.MapGet("/scrape/btmc/health", (IBtmcScraper scraper) =>
    {
      var health = scraper.GetHealth();
      return Results.Json(new
      {
        health.LastSuccess,
        health.LastFailure,
        health.LastError,
        health.ConsecutiveFailures,
        health.LastInserted,
        health.TotalInserted,
        health.TotalRuns,
        health.LastAnomalyCount,
        health.LastAnomalySummary
      });
    }).WithTags("Admin");

    // POST /admin/snapshot/daily?date=YYYY-MM-DD
    group.MapPost("/snapshot/daily", async (string? date, IDailySnapshotRepository snapshotRepo, CancellationToken ct) =>
    {
      DateOnly targetDate;
      if (string.IsNullOrWhiteSpace(date))
      {
        // Default to today in Asia/Ho_Chi_Minh
        var tz = TimeZoneInfo.FindSystemTimeZoneById(Environment.GetEnvironmentVariable("TZ") ?? "Asia/Ho_Chi_Minh");
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        targetDate = DateOnly.FromDateTime(localNow.DateTime);
      }
      else
      {
        if (!DateOnly.TryParse(date, out targetDate))
          return Results.BadRequest(new { error = "date must be in YYYY-MM-DD format" });
      }

      await snapshotRepo.UpsertDailyCloseAsync(targetDate, ct);
      return Results.Json(new { date = targetDate.ToString("yyyy-MM-dd"), status = "completed" });
    }).WithTags("Admin");

    // POST /admin/alerts/evaluate
    group.MapPost("/alerts/evaluate", async (IAlertEvaluator evaluator, GoldTracker.Infrastructure.Config.AlertsOptions options, CancellationToken ct) =>
    {
      var priceJump = await evaluator.EvaluatePriceJumpAsync(options.PriceJumpPercent, ct);
      var noData = await evaluator.EvaluateNoDataAsync(options.NoDataMinutes, ct);
      var scraperErrors = await evaluator.EvaluateScraperErrorsAsync(ct);
      
      return Results.Json(new
      {
        priceJump = priceJump.Select(a => new { a.Kind, a.Brand, a.Region, a.ProductKind, a.Message, a.Severity }),
        noData = noData.Select(a => new { a.Kind, a.Message, a.Severity }),
        scraperErrors = scraperErrors.Select(a => new { a.Kind, a.Message, a.Severity }),
        total = priceJump.Count + noData.Count + scraperErrors.Count
      });
    }).WithTags("Admin");

    // POST /admin/alerts/dispatch
    group.MapPost("/alerts/dispatch", async (
      IAlertEvaluator evaluator,
      ITelegramNotifier notifier,
      IAlertEventRepository alertRepo,
      GoldTracker.Infrastructure.Config.AlertsOptions alertsOptions,
      Microsoft.Extensions.Options.IOptions<GoldTracker.Infrastructure.Config.TelegramOptions> telegramOptions,
      CancellationToken ct) =>
    {
      if (!telegramOptions.Value.Enabled || string.IsNullOrEmpty(telegramOptions.Value.DefaultChatId))
        return Results.BadRequest(new { error = "Telegram not enabled or chat ID not configured" });

      var priceJump = await evaluator.EvaluatePriceJumpAsync(alertsOptions.PriceJumpPercent, ct);
      var noData = await evaluator.EvaluateNoDataAsync(alertsOptions.NoDataMinutes, ct);
      var scraperErrors = await evaluator.EvaluateScraperErrorsAsync(ct);
      
      var allCandidates = priceJump.Concat(noData).Concat(scraperErrors).ToList();
      var sent = 0;

      foreach (var candidate in allCandidates)
      {
        try
        {
          if (!string.IsNullOrEmpty(candidate.DedupKey))
          {
            var existing = await alertRepo.GetLastByDedupKeyAsync(candidate.DedupKey, TimeSpan.FromMinutes(30), ct);
            if (existing != null && existing.Severity == candidate.Severity)
              continue;
          }

          var message = FormatAlertMessage(candidate);
          await notifier.SendTextAsync(telegramOptions.Value.DefaultChatId, message, ct);
          
          var alertEvent = new GoldTracker.Domain.Alerts.AlertEvent
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
          await alertRepo.AddAsync(alertEvent, ct);
          sent++;
        }
        catch (Exception)
        {
          // Log but continue
        }
      }

      return Results.Json(new { sent, total = allCandidates.Count });
    }).WithTags("Admin");

    // POST /admin/brief/today
    group.MapPost("/brief/today", async (
      DailyBriefService briefService,
      CancellationToken ct) =>
    {
      await briefService.SendDailyBriefAsync(ct);
      return Results.Json(new { status = "sent" });
    }).WithTags("Admin");
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

