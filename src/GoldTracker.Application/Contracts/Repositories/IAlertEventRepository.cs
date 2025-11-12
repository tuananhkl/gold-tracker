using GoldTracker.Domain.Alerts;

namespace GoldTracker.Application.Contracts.Repositories;

public interface IAlertEventRepository
{
  Task AddAsync(AlertEvent alert, CancellationToken ct = default);
  Task<AlertEvent?> GetLastByDedupKeyAsync(string dedupKey, TimeSpan window, CancellationToken ct = default);
}

