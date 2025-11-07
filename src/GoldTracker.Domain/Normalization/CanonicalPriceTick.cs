namespace GoldTracker.Domain.Normalization;

public sealed record CanonicalPriceTick
{
  public Guid ProductId { get; init; }
  public Guid SourceId { get; init; }
  public decimal PriceBuy { get; init; }
  public decimal PriceSell { get; init; }
  public string Currency { get; init; } = "VND";
  public DateTimeOffset CollectedAt { get; init; }
  public DateTimeOffset EffectiveAt { get; init; }
  public string RawHash { get; init; } = string.Empty;
}

