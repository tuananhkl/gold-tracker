namespace GoldTracker.Infrastructure.Scrapers.Doji;

public sealed class DojiOptions
{
  public const string SectionName = "Doji";
  public string BaseUrl { get; set; } = "https://doji.vn";
  public string PriceEndpoint { get; set; } = "/gia-vang";
  public string[] Regions { get; set; } = ["Hanoi", "HCMC"];
  public int TimeoutSeconds { get; set; } = 10;
}

