using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

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
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .Enrich.With(new VietnamTimeEnricher())
        .WriteTo.Console(new RenderedCompactJsonFormatter());
    });
  }
}


