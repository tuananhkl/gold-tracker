using GoldTracker.Application.DTOs;

namespace GoldTracker.Application.Contracts;

public interface ISourceQuery
{
  Task<SourceHealthDto> GetHealthAsync(CancellationToken ct = default);
}
