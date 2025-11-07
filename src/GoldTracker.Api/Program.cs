using GoldTracker.Api.Endpoints;
using GoldTracker.Application.Contracts;
using GoldTracker.Infrastructure.DI;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Docker-specific configuration if running in Docker
var env = builder.Environment.EnvironmentName;
if (env == "Docker" || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
{
  builder.Configuration.AddJsonFile("appsettings.Docker.json", optional: true, reloadOnChange: true);
}

builder.Host.UseSerilog((ctx, lc) => lc
  .ReadFrom.Configuration(ctx.Configuration)
  .WriteTo.Console());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGoldTrackerCore(builder.Configuration);
builder.Services.AddDojiScraper(builder.Configuration);
builder.Services.AddScheduling();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Enable Swagger in Development and Docker environments
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
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
    // Try to connect to DB using same logic as DI
    var connString = Environment.GetEnvironmentVariable("POSTGRES_CONN")
      ?? app.Configuration.GetConnectionString("Postgres")
      ?? app.Configuration.GetSection("ConnectionStrings:Postgres").Value
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

// Admin endpoints
app.MapAdminEndpoints();

// V1 Public API endpoints
app.MapPricesV1Endpoints();

app.Run();

public partial class Program { }
