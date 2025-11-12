namespace GoldTracker.Application.Contracts.Alerts;

public sealed record PricePoint
{
  public DateTimeOffset Timestamp { get; init; }
  public decimal PriceBuy { get; init; }
  public decimal PriceSell { get; init; }
}

public interface IPriceWindowReader
{
  Task<IReadOnlyList<PricePoint>> GetLastHourSeriesAsync(string? brand, string? region, string? productKind, CancellationToken ct = default);
  Task<PricePoint?> GetLatestSnapshotAsync(string? kind, string? brand, string? region, CancellationToken ct = default);
}

