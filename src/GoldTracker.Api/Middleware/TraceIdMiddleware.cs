using System.Diagnostics;

namespace GoldTracker.Api.Middleware;

/// <summary>
/// Middleware to create and propagate trace IDs for request correlation
/// </summary>
public sealed class TraceIdMiddleware
{
  private readonly RequestDelegate _next;
  private const string TraceIdHeader = "x-trace-id";
  private const string CorrelationIdHeader = "x-correlation-id";

  public TraceIdMiddleware(RequestDelegate next)
  {
    _next = next;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    // Get or create trace ID
    var traceId = context.Request.Headers[TraceIdHeader].FirstOrDefault()
      ?? context.TraceIdentifier
      ?? Activity.Current?.TraceId.ToString()
      ?? Guid.NewGuid().ToString("N");

    // Get or create correlation ID
    var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
      ?? traceId;

    // Set in response headers for client propagation
    context.Response.Headers[TraceIdHeader] = traceId;
    context.Response.Headers[CorrelationIdHeader] = correlationId;

    // Store in HttpContext.Items for logging
    context.Items["TraceId"] = traceId;
    context.Items["CorrelationId"] = correlationId;

    // Set Activity trace ID if available
    if (Activity.Current != null)
    {
      Activity.Current.SetTag("traceId", traceId);
      Activity.Current.SetTag("correlationId", correlationId);
    }

    await _next(context);
  }
}

