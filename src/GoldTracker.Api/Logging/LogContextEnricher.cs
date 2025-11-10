using Serilog.Context;

namespace GoldTracker.Api.Logging;

public static class LogContextEnricher
{
  public static IDisposable Push(HttpContext ctx, string appName, string env, string serviceVersion)
  {
    var hostname = Environment.MachineName;
    var clientIp = ctx.Connection.RemoteIpAddress?.ToString();
    var userAgent = ctx.Request.Headers.UserAgent.ToString();

    return new CompositeDisposable(new[]
    {
      LogContext.PushProperty("app", appName, true),
      LogContext.PushProperty("env", env, true),
      LogContext.PushProperty("service_version", serviceVersion, true),
      LogContext.PushProperty("hostname", hostname, true),
      LogContext.PushProperty("client_ip", clientIp ?? string.Empty, true),
      LogContext.PushProperty("user_agent", userAgent ?? string.Empty, true)
    });
  }

  private sealed class CompositeDisposable : IDisposable
  {
    private readonly IDisposable[] _disposables;
    public CompositeDisposable(IDisposable[] items) => _disposables = items;
    public void Dispose()
    {
      foreach (var d in _disposables) d.Dispose();
    }
  }
}


