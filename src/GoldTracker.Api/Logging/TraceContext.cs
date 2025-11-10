using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace GoldTracker.Api.Logging;

public static class TraceContext
{
  private const string TraceHeader = "X-Trace-Id";
  private const string CorrelationHeader = "X-Correlation-Id";
  private static readonly char[] Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

  private static string RandomToken(int bytes, int outLen)
  {
    Span<byte> buf = stackalloc byte[bytes];
    RandomNumberGenerator.Fill(buf);
    var sb = new StringBuilder(outLen);
    int idx = 0;
    while (sb.Length < outLen)
    {
      var b = buf[idx++ % buf.Length];
      sb.Append(Base32[b % Base32.Length]);
    }
    return sb.ToString();
  }

  public static (string traceId, string correlationId, string contextId, string spanId) Ensure(HttpContext ctx)
  {
    var traceId = ctx.Request.Headers[TraceHeader].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(traceId))
    {
      traceId = RandomToken(8, 10);
    }
    var correlation = ctx.Request.Headers[CorrelationHeader].FirstOrDefault() ?? traceId;
    var contextId = RandomToken(4, 5);

    var span = Activity.Current ?? new Activity("http-request").Start();
    var spanId = span.SpanId.ToString();

    ctx.Items["trace_id"] = traceId;
    ctx.Items["correlation_id"] = correlation;
    ctx.Items["context_id"] = contextId;
    ctx.Items["span_id"] = spanId;

    ctx.Response.Headers[TraceHeader] = traceId;
    ctx.Response.Headers[CorrelationHeader] = correlation;

    return (traceId, correlation, contextId, spanId);
  }
}


