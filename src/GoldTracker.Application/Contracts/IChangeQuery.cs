using GoldTracker.Application.DTOs;

namespace GoldTracker.Application.Contracts;

public interface IChangeQuery
{
  Task<DayChangeDto> GetChangesAsync(string? kind, string? brand, string? region, CancellationToken ct = default);
}
