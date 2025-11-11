using GoldTracker.Infrastructure.Scrapers;

namespace GoldTracker.Infrastructure.Scrapers.Btmc;

public interface IBtmcScraper
{
  Task<int> RunOnceAsync(CancellationToken ct = default);
  ScraperHealthSnapshot GetHealth();
}

