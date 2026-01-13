using Serilog;
using Serilog.Events;

namespace GoldTracker.Api.Logging;

public static class SerilogConfig
{
  public static void AddSerilogLogging(this WebApplicationBuilder builder)
  {
    builder.Logging.ClearProviders();
    var env = builder.Environment.EnvironmentName;

    builder.Host.UseSerilog((ctx, lc) =>
    {
      lc.MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With(new VietnamTimeEnricher())
        // Ensure '@l' (log level) is always present (including Information) to support Kibana filtering.
        .WriteTo.Console(new AlwaysLevelRenderedCompactJsonFormatter());
    });
  }
}


