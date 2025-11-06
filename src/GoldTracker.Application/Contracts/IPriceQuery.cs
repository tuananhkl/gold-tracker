using GoldTracker.Application.DTOs;

namespace GoldTracker.Application.Contracts;

public interface IPriceQuery
{
  Task<LatestPriceDto> GetLatestAsync(string? kind, string? brand, string? region, CancellationToken ct = default);
  Task<(DateOnly from, DateOnly to, IReadOnlyList<HistoryPointDto> points)> GetHistoryAsync(string? kind, int days, string? brand, string? region, CancellationToken ct = default);
}
