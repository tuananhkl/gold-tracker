namespace GoldTracker.Application.Contracts.Alerts;

public interface IScraperHealthReader
{
  Task<bool> HasErrorsAsync(TimeSpan within, CancellationToken ct = default);
  Task<bool> HasNoDataAsync(TimeSpan within, CancellationToken ct = default);
}

