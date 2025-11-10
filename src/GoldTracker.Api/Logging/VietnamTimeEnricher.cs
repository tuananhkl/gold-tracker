using Serilog.Core;
using Serilog.Events;

namespace GoldTracker.Api.Logging;

public sealed class VietnamTimeEnricher : ILogEventEnricher
{
  private readonly TimeZoneInfo _vnTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

  public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
  {
    var nowVn = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _vnTz);
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ts_vn", nowVn.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK")));
  }
}


