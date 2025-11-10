namespace GoldTracker.Api.Logging;

public static class BusinessKeyExtractor
{
  public static object Extract(HttpContext ctx)
  {
    var path = ctx.Request.Path.HasValue ? ctx.Request.Path.Value! : "/";
    var method = ctx.Request.Method;
    var biz = path.StartsWith("/api/prices", StringComparison.OrdinalIgnoreCase) ? "Prices"
            : path.StartsWith("/api/sources", StringComparison.OrdinalIgnoreCase) ? "Sources"
            : path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ? "Snapshots"
            : "Application";

    var query = ctx.Request.Query.ToDictionary(k => k.Key, v => (object?)v.Value.ToString());
    return new
    {
      endpoint = path,
      method,
      biz_func = biz,
      query_keys = query
    };
  }
}


