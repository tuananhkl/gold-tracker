using Serilog;

namespace GoldTracker.Api.Logging;

public static class AppLog
{
  public static void SecurityAudit(string message, object? keys = null) =>
    Log.Information("{message} {message_type} {@biz_keys}", message, "SecurityAudit", keys ?? new { });

  public static void Integration(string message, object? keys = null) =>
    Log.Information("{message} {message_type} {@biz_keys}", message, "Integration", keys ?? new { });

  public static void BizInfo(string message, object? keys = null) =>
    Log.Information("{message} {message_type} {@biz_keys}", message, "Application", keys ?? new { });
}


