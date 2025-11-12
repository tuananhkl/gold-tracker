namespace GoldTracker.Infrastructure.Scrapers.PhucThanh;

public sealed class PhucThanhOptions
{
  public const string SectionName = "PhucThanh";

  public string BaseUrl { get; set; } = "https://vangbacphucthanh.vn/";
  public int TimeoutSeconds { get; set; } = 15;
  public int RetryCount { get; set; } = 3;

  public decimal MaxSpreadRatio { get; set; } = 0.2m;
  public decimal MinPrice { get; set; } = 9_000_000m;
  public decimal MaxPrice { get; set; } = 220_000_000m;
}


