namespace GoldTracker.Domain.Normalization;

public sealed record RawPriceRecord
{
  public string SourceName { get; init; } = string.Empty;
  public string? Brand { get; init; }
  public string? Form { get; init; }
  public string? Karat { get; init; }
  public string? Region { get; init; }
  public decimal? PriceBuy { get; init; }
  public decimal? PriceSell { get; init; }
  public string? Currency { get; init; }
  public DateTimeOffset? CollectedAt { get; init; }
  public DateTimeOffset? EffectiveAt { get; init; }
}

