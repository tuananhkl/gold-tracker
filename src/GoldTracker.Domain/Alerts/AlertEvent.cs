namespace GoldTracker.Domain.Alerts;

public sealed record AlertEvent
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
  public string Kind { get; init; } = string.Empty; // price_jump | no_data | scraper_error
  public string? Brand { get; init; }
  public string? Region { get; init; }
  public string? ProductKind { get; init; } // "ring" | "bar" | ...
  public string Message { get; init; } = string.Empty;
  public string Severity { get; init; } = "info"; // info | warn | high
  public Dictionary<string, object> Meta { get; init; } = new();
  public string? DedupKey { get; init; }
}

