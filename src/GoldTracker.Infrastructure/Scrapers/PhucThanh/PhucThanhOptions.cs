namespace GoldTracker.Infrastructure.Scrapers.PhucThanh;

public sealed class PhucThanhOptions
{
  public const string SectionName = "PhucThanh";

  public string BaseUrl { get; set; } = "https://vangbacphucthanh.vn/";
  public int TimeoutSeconds { get; set; } = 15;
  public int RetryCount { get; set; } = 3;

  public decimal MaxSpreadRatio { get; set; } = 0.2m;
  // Prices are stored in VND to match other brands (SJC, DOJI, BTMC all use VND)
  public decimal MinPrice { get; set; } = 9_000_000m;  // 9 million VND
  public decimal MaxPrice { get; set; } = 220_000_000m; // 220 million VND
}


