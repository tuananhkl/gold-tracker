using GoldTracker.Application.Contracts;
using GoldTracker.Application.Queries;

namespace GoldTracker.Application.Contracts;

public interface IPriceV1Query
{
  Task<LatestPricesResponse> GetLatestAsync(LatestQuery query, CancellationToken ct = default);
  Task<PriceHistoryResponse> GetHistoryAsync(HistoryQuery query, CancellationToken ct = default);
  Task<PriceChangesResponse> GetChangesAsync(ChangesQuery query, CancellationToken ct = default);
}

