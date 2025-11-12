namespace GoldTracker.Application.Contracts.Alerts;

public sealed record AlertCandidate
{
  public string Kind { get; init; } = string.Empty;
  public string? Brand { get; init; }
  public string? Region { get; init; }
  public string? ProductKind { get; init; }
  public string Message { get; init; } = string.Empty;
  public string Severity { get; init; } = "info";
  public Dictionary<string, object> Meta { get; init; } = new();
  public string? DedupKey { get; init; }
}

