using GoldTracker.Api.Endpoints;
using GoldTracker.Api.Middleware;
using GoldTracker.Application.Contracts;
using GoldTracker.Infrastructure.DI;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Serilog;
using Serilog.Events;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add Docker-specific configuration if running in Docker
var environmentName = builder.Environment.EnvironmentName;
if (environmentName == "Docker" || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
{
  builder.Configuration.AddJsonFile("appsettings.Docker.json", optional: true, reloadOnChange: true);
}

// Get service version from environment or default
var serviceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION")
  ?? Environment.GetEnvironmentVariable("GIT_SHA")
  ?? Environment.GetEnvironmentVariable("BUILD_VERSION")
  ?? "unknown";

var appName = Environment.GetEnvironmentVariable("APP_NAME") ?? "gold-tracker-api";
var env = environmentName;

// Serilog configuration - HttpContextEnricher will be added after app build
builder.Host.UseSerilog((ctx, lc) => lc
  .ReadFrom.Configuration(ctx.Configuration)
  .Enrich.FromLogContext()
  .Enrich.WithProperty("app", appName)
  .Enrich.WithProperty("env", env)
  .Enrich.WithProperty("service_version", serviceVersion)
  .Enrich.WithMachineName()
  .Enrich.WithThreadId()
  .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

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

// Add HttpContextAccessor for logging enricher
builder.Services.AddHttpContextAccessor();

// Register HttpContextEnricher for Serilog
builder.Services.AddSingleton<GoldTracker.Api.HttpContextEnricher>();

builder.Services.AddGoldTrackerCore(builder.Configuration);
builder.Services.AddDojiScraper(builder.Configuration);
builder.Services.AddScheduling();

var app = builder.Build();

// Reconfigure Serilog with HttpContextEnricher after app is built
var httpContextAccessor = app.Services.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
var httpContextEnricher = new GoldTracker.Api.HttpContextEnricher(httpContextAccessor);
Log.Logger = new LoggerConfiguration()
  .ReadFrom.Configuration(app.Configuration)
  .Enrich.FromLogContext()
  .Enrich.WithProperty("app", appName)
  .Enrich.WithProperty("env", env)
  .Enrich.WithProperty("service_version", serviceVersion)
  .Enrich.WithMachineName()
  .Enrich.WithThreadId()
  .Enrich.With(httpContextEnricher)
  .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
  .CreateLogger();

// Add trace ID middleware before request logging
app.UseMiddleware<TraceIdMiddleware>();

// Configure Serilog request logging with structured fields
app.UseSerilogRequestLogging(options =>
{
  options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
  options.GetLevel = (httpContext, elapsed, ex) => ex != null
    ? LogEventLevel.Error
    : elapsed > 5000
      ? LogEventLevel.Warning
      : LogEventLevel.Information;
  options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
  {
    diagnosticContext.Set("http.method", httpContext.Request.Method);
    diagnosticContext.Set("http.path", httpContext.Request.Path);
    diagnosticContext.Set("http.status_code", httpContext.Response.StatusCode);
    diagnosticContext.Set("http.scheme", httpContext.Request.Scheme);
    diagnosticContext.Set("http.host", httpContext.Request.Host.Value);
    diagnosticContext.Set("http.user_agent", httpContext.Request.Headers["User-Agent"].ToString());
    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
    if (!string.IsNullOrEmpty(remoteIp))
      diagnosticContext.Set("http.remote_ip", remoteIp);
    
    // Get trace/correlation IDs from context
    if (httpContext.Items.TryGetValue("TraceId", out var traceId) && traceId != null)
      diagnosticContext.Set("traceId", traceId);
    if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId) && correlationId != null)
      diagnosticContext.Set("correlationId", correlationId);
    
    // Get user ID if available (from claims or headers)
    var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
      ?? httpContext.Request.Headers["x-user-id"].FirstOrDefault();
    if (!string.IsNullOrEmpty(userId))
      diagnosticContext.Set("userId", userId!);
  };
});

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
app.MapGet("/healthz", (ILogger<Program> logger) =>
{
  logger.LogInformation("Health check requested");
  return Results.Json(new { status = "healthy" });
})
  .WithTags("Ops");

app.MapGet("/readyz", async (ILogger<Program> logger) =>
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
    logger.LogInformation("Readiness check passed - database connection successful");
    return Results.Ok();
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Readiness check failed - database connection error");
    return Results.StatusCode(503);
  }
}).WithTags("Ops");

// Test endpoint for logging verification (generates INFO and ERROR logs)
app.MapGet("/api/test/logging", (ILogger<Program> logger) =>
{
  var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "test-trace";
  
  logger.LogInformation("Test INFO log - logging system verification");
  
  try
  {
    throw new InvalidOperationException("Test ERROR log - exception handling verification");
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Test ERROR log with stack trace");
  }
  
  return Results.Json(new 
  { 
    message = "Logging test completed",
    traceId = traceId,
    timestamp = DateTime.UtcNow
  });
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
