namespace GoldTracker.Infrastructure.Scrapers.Sjc;

public sealed class SjcOptions
{
  public const string SectionName = "Sjc";
  public string PriceUrl { get; set; } = "https://sjc.com.vn/GoldPrice/Services/PriceService.ashx";
  public int TimeoutSeconds { get; set; } = 15;
  public int RetryCount { get; set; } = 3;
  public decimal MaxSpreadRatio { get; set; } = 0.15m;
  public decimal MinPrice { get; set; } = 9_000_000m;
  public decimal MaxPrice { get; set; } = 220_000_000m;
}

