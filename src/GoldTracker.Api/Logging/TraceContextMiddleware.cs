using Serilog;

namespace GoldTracker.Api.Logging;

public sealed class TraceContextMiddleware
{
  private readonly RequestDelegate _next;
  private readonly string _app;
  private readonly string _env;
  private readonly string _version;

  public TraceContextMiddleware(RequestDelegate next, IConfiguration cfg, IWebHostEnvironment env)
  {
    _next = next;
    _app = Environment.GetEnvironmentVariable("APP_ID") ?? "gold-tracker-api";
    _env = env.EnvironmentName;
    _version = typeof(TraceContextMiddleware).Assembly.GetName().Version?.ToString() ?? "unknown";
  }

  public async Task InvokeAsync(HttpContext ctx)
  {
    var (traceId, correlationId, contextId, spanId) = TraceContext.Ensure(ctx);
    using var push = LogContextEnricher.Push(ctx, _app, _env, _version);

    using (Serilog.Context.LogContext.PushProperty("trace_id", traceId, true))
    using (Serilog.Context.LogContext.PushProperty("correlation_id", correlationId, true))
    using (Serilog.Context.LogContext.PushProperty("context_id", contextId, true))
    using (Serilog.Context.LogContext.PushProperty("span_id", spanId, true))
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      var endpoint = ctx.Request.Path.HasValue ? ctx.Request.Path.Value! : "/";
      var method = ctx.Request.Method;
      var bizKeys = BusinessKeyExtractor.Extract(ctx);

      using (Serilog.Context.LogContext.PushProperty("message_type", "Application", true))
      using (Serilog.Context.LogContext.PushProperty("endpoint", endpoint, true))
      using (Serilog.Context.LogContext.PushProperty("method", method, true))
      using (Serilog.Context.LogContext.PushProperty("biz_keys", bizKeys, true))
      {
        try
        {
          Log.Information("RequestStarted");
          await _next(ctx);
          sw.Stop();
          Log.Information("RequestCompleted {status} {duration_ms}",
            ctx.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
          sw.Stop();
          Log.Error(ex, "RequestFailed {status} {duration_ms}",
            500, sw.Elapsed.TotalMilliseconds);
          throw;
        }
      }
    }
  }
}


