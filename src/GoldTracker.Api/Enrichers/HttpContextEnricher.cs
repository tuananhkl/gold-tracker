using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace GoldTracker.Api;

/// <summary>
/// Serilog enricher to add trace/correlation IDs from HttpContext
/// </summary>
public sealed class HttpContextEnricher : ILogEventEnricher
{
  private readonly IHttpContextAccessor _httpContextAccessor;

  public HttpContextEnricher(IHttpContextAccessor httpContextAccessor)
  {
    _httpContextAccessor = httpContextAccessor;
  }

  public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
  {
    var httpContext = _httpContextAccessor.HttpContext;
    if (httpContext == null) return;

    // Add trace ID
    if (httpContext.Items.TryGetValue("TraceId", out var traceId))
    {
      var traceIdProperty = propertyFactory.CreateProperty("traceId", traceId);
      logEvent.AddPropertyIfAbsent(traceIdProperty);
    }

    // Add correlation ID
    if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
    {
      var correlationIdProperty = propertyFactory.CreateProperty("correlationId", correlationId);
      logEvent.AddPropertyIfAbsent(correlationIdProperty);
    }

    // Add span ID from Activity if available
    var activity = Activity.Current;
    if (activity != null)
    {
      var spanIdProperty = propertyFactory.CreateProperty("spanId", activity.SpanId.ToString());
      logEvent.AddPropertyIfAbsent(spanIdProperty);
    }
  }
}

