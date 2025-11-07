namespace GoldTracker.Infrastructure.Scrapers.Doji;

public interface IDojiScraper
{
  Task<int> RunOnceAsync(CancellationToken ct = default);
}

