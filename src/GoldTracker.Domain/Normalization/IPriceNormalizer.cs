namespace GoldTracker.Domain.Normalization;

public interface IPriceNormalizer
{
  Task<(Guid ProductId, Guid SourceId, CanonicalPriceTick Tick)> NormalizeAsync(
    RawPriceRecord raw,
    CancellationToken ct = default);
}

