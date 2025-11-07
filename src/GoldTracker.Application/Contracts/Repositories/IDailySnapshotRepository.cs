namespace GoldTracker.Application.Contracts.Repositories;

public interface IDailySnapshotRepository
{
  Task UpsertDailyCloseAsync(DateOnly localDate, CancellationToken ct = default);
}

