using GoldTracker.Domain.Normalization;

namespace GoldTracker.Application.Contracts.Repositories;

public interface IPriceTickRepository
{
  Task InsertAsync(CanonicalPriceTick tick, CancellationToken ct = default);
  Task<IReadOnlyList<CanonicalPriceTick>> GetLatestAsync(string? kind, string? brand, string? region, CancellationToken ct = default);
  Task<IReadOnlyList<(DateOnly Date, decimal PriceSell)>> GetHistoryAsync(string? kind, int days, string? brand, string? region, CancellationToken ct = default);
  Task<IReadOnlyList<(DateOnly Date, decimal PriceSellClose, decimal DeltaVsYesterday, string Direction)>> GetDayOverDayAsync(string? kind, string? brand, string? region, CancellationToken ct = default);
}

