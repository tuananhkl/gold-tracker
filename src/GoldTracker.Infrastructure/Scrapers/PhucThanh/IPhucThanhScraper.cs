namespace GoldTracker.Infrastructure.Scrapers.PhucThanh;

public interface IPhucThanhScraper
{
  Task<int> RunOnceAsync(CancellationToken ct = default);
  ScraperHealthSnapshot GetHealth();
}


