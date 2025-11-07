using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Infrastructure.Scrapers.Doji;
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

