using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Infrastructure.Scrapers.Btmc;
using GoldTracker.Infrastructure.Scrapers.Doji;
using GoldTracker.Infrastructure.Scrapers.Sjc;
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
  }
}

