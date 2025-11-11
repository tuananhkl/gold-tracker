using System.Collections.Concurrent;

namespace GoldTracker.Infrastructure.Scrapers;

public sealed record ScraperHealthSnapshot(
  DateTimeOffset? LastSuccess,
  DateTimeOffset? LastFailure,
  string? LastError,
  int ConsecutiveFailures,
  int LastInserted,
  int TotalInserted,
  int TotalRuns,
  int LastAnomalyCount,
  string? LastAnomalySummary);

public sealed class ScraperHealthTracker
{
  private readonly object _lock = new();

  private DateTimeOffset? _lastSuccess;
  private DateTimeOffset? _lastFailure;
  private string? _lastError;
  private int _consecutiveFailures;
  private int _lastInserted;
  private int _totalInserted;
  private int _totalRuns;
  private int _lastAnomalyCount;
  private string? _lastAnomalySummary;

  public void RecordSuccess(int inserted, int anomalyCount, string? anomalySummary)
  {
    lock (_lock)
    {
      _lastSuccess = DateTimeOffset.UtcNow;
      _lastInserted = inserted;
      _totalInserted += inserted;
      _consecutiveFailures = 0;
      _lastAnomalyCount = anomalyCount;
      _lastAnomalySummary = string.IsNullOrWhiteSpace(anomalySummary) ? null : anomalySummary;
      _totalRuns++;
      _lastError = null;
    }
  }

  public void RecordFailure(Exception ex)
  {
    RecordFailure(ex.Message);
  }

  public void RecordFailure(string message)
  {
    lock (_lock)
    {
      _lastFailure = DateTimeOffset.UtcNow;
      _lastError = message;
      _consecutiveFailures++;
      _totalRuns++;
    }
  }

  public ScraperHealthSnapshot Snapshot()
  {
    lock (_lock)
    {
      return new ScraperHealthSnapshot(
        _lastSuccess,
        _lastFailure,
        _lastError,
        _consecutiveFailures,
        _lastInserted,
        _totalInserted,
        _totalRuns,
        _lastAnomalyCount,
        _lastAnomalySummary);
    }
  }
}

