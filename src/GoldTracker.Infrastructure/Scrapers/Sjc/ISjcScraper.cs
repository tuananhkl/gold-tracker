using GoldTracker.Infrastructure.Scrapers;

namespace GoldTracker.Infrastructure.Scrapers.Sjc;

public interface ISjcScraper
{
  Task<int> RunOnceAsync(CancellationToken ct = default);
  ScraperHealthSnapshot GetHealth();
}

