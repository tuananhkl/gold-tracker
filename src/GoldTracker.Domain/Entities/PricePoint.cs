namespace GoldTracker.Domain.Entities;

using GoldTracker.Domain.ValueObjects;

public sealed class PricePoint
{
  public Guid ProductId { get; init; }
  public Guid SourceId { get; init; }
  public DateTimeOffset EffectiveAt { get; init; }
  public Money PriceBuy { get; init; } = new(0m, "VND");
  public Money PriceSell { get; init; } = new(0m, "VND");
}
