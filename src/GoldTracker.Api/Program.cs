using GoldTracker.Application.Contracts;
using GoldTracker.Infrastructure.DI;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
  .ReadFrom.Configuration(ctx.Configuration)
  .WriteTo.Console());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGoldTrackerCore(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

// Health endpoints
app.MapGet("/healthz", () => Results.Json(new { status = "healthy" }))
  .WithTags("Ops");

app.MapGet("/readyz", async () =>
{
  try
  {
    // Try to connect to DB
    var connString = builder.Configuration.GetConnectionString("Postgres")
      ?? Environment.GetEnvironmentVariable("POSTGRES_CONN")
      ?? "Host=localhost;Port=5432;Username=gold;Password=gold;Database=gold";
    using var conn = new Npgsql.NpgsqlConnection(connString);
    await conn.OpenAsync();
    await conn.CloseAsync();
    return Results.Ok();
  }
  catch
  {
    return Results.StatusCode(503);
  }
}).WithTags("Ops");

// Prices endpoints
app.MapGet("/api/prices/latest", async (string? kind, string? brand, string? region, IPriceQuery svc, CancellationToken ct) =>
{
  var dto = await svc.GetLatestAsync(kind, brand, region, ct);
  return Results.Json(dto);
}).WithTags("Prices");

app.MapGet("/api/prices/history", async (string? kind, int days, string? brand, string? region, IPriceQuery svc, CancellationToken ct) =>
{
  if (days is not (7 or 30 or 90 or 180)) return Results.BadRequest(new { error = "days must be one of 7,30,90,180" });
  var (from, to, points) = await svc.GetHistoryAsync(kind, days, brand, region, ct);
  return Results.Json(new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), points });
}).WithTags("Prices");

app.MapGet("/api/prices/changes", async (string? kind, string? brand, string? region, IChangeQuery svc, CancellationToken ct) =>
{
  var dto = await svc.GetChangesAsync(kind, brand, region, ct);
  return Results.Json(dto);
}).WithTags("Prices");

// Sources
app.MapGet("/api/sources/health", async (ISourceQuery svc, CancellationToken ct) =>
{
  var dto = await svc.GetHealthAsync(ct);
  return Results.Json(dto);
}).WithTags("Sources");

app.Run();

public partial class Program { }
