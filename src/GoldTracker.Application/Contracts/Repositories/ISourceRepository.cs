using GoldTracker.Domain.Entities;

namespace GoldTracker.Application.Contracts.Repositories;

public interface ISourceRepository
{
  Task<Source?> GetByNameAsync(string name, CancellationToken ct = default);
  Task<Source> EnsureAsync(string name, string baseUrl, CancellationToken ct = default);
}

