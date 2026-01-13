using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace GoldTracker.Api.Logging;

/// <summary>
/// Wrapper around Serilog's <see cref="RenderedCompactJsonFormatter"/> that always emits <c>@l</c>
/// (log level) even for Information events (which Serilog's compact format omits by default).
/// This makes it easier to filter by log level in Kibana/Elastic.
/// </summary>
public sealed class AlwaysLevelRenderedCompactJsonFormatter : ITextFormatter
{
  private readonly RenderedCompactJsonFormatter _inner = new();

  public void Format(LogEvent logEvent, TextWriter output)
  {
    // Format with the built-in compact formatter first, then inject @l if missing.
    using var sw = new StringWriter(CultureInfo.InvariantCulture);
    _inner.Format(logEvent, sw);
    var json = sw.ToString();

    // Normalize level token to a single convention, so Kibana filtering is consistent.
    // ECS convention: trace/debug/info/warn/error/fatal
    var levelToken = ToLevelToken(logEvent.Level);

    // Compact JSON includes @l for non-Information levels, and omits it for Information.
    // We always ensure @l exists and matches our token.
    if (json.Contains("\"@l\"", StringComparison.Ordinal))
    {
      output.Write(ReplaceLevelToken(json, levelToken));
      return;
    }

    var braceIndex = json.IndexOf('{');
    if (braceIndex < 0)
    {
      output.Write(json);
      return;
    }

    // Insert right after '{' to keep JSON valid and stable for downstream parsing.
    // Example: {"@t":"...","@mt":"..."} -> {"@l":"Information","@t":"...","@mt":"..."}
    var insert = $"\"@l\":\"{levelToken}\",";
    output.Write(json.AsSpan(0, braceIndex + 1));
    output.Write(insert);
    output.Write(json.AsSpan(braceIndex + 1));
  }

  private static string ToLevelToken(LogEventLevel level) =>
    level switch
    {
      LogEventLevel.Verbose => "trace",
      LogEventLevel.Debug => "debug",
      LogEventLevel.Information => "info",
      LogEventLevel.Warning => "warn",
      LogEventLevel.Error => "error",
      LogEventLevel.Fatal => "fatal",
      _ => "info",
    };

  private static string ReplaceLevelToken(string json, string levelToken)
  {
    // Replace value of "@l":"..." with "@l":"{levelToken}" using simple substring operations.
    const string key = "\"@l\":\"";
    var start = json.IndexOf(key, StringComparison.Ordinal);
    if (start < 0)
    {
      return json;
    }

    var valueStart = start + key.Length;
    var valueEnd = json.IndexOf('"', valueStart);
    if (valueEnd < 0)
    {
      return json;
    }

    return string.Concat(
      json.AsSpan(0, valueStart),
      levelToken,
      json.AsSpan(valueEnd));
  }
}

