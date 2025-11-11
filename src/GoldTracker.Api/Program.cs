using GoldTracker.Api.Endpoints;
using GoldTracker.Application.Contracts;
using GoldTracker.Infrastructure.DI;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using GoldTracker.Api.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Logging.json", optional: true, reloadOnChange: true);
builder.AddSerilogLogging();

// Add Docker-specific configuration if running in Docker
var env = builder.Environment.EnvironmentName;
if (env == "Docker" || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
{
  builder.Configuration.AddJsonFile("appsettings.Docker.json", optional: true, reloadOnChange: true);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
  // Add server with base path for Ingress
  options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
  {
    Url = "/gold",
    Description = "Gold Tracker API (via Ingress)"
  });
});

builder.Services.AddGoldTrackerCore(builder.Configuration);
builder.Services.AddDojiScraper(builder.Configuration);
builder.Services.AddBtmcScraper(builder.Configuration);
builder.Services.AddSjcScraper(builder.Configuration);
builder.Services.AddScheduling();

var app = builder.Build();

app.UseMiddleware<TraceContextMiddleware>();
app.UseSerilogRequestLogging();

// Enable Swagger in Development and Docker environments
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
  app.UseSwagger(options =>
  {
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
  });
  app.UseSwaggerUI(options =>
  {
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "GoldTracker.Api v1");
    options.RoutePrefix = "swagger";
    // Configure Swagger UI to use server URL from swagger.json
    options.ConfigObject.Urls = new[] { new Swashbuckle.AspNetCore.SwaggerUI.UrlDescriptor 
    { 
      Name = "GoldTracker.Api v1", 
      Url = "/swagger/v1/swagger.json" 
    }};
    // Enable deep linking and use server URL
    options.EnableDeepLinking();
    options.EnableFilter();
  });
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
