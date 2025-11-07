namespace GoldTracker.Infrastructure.Scrapers.Doji;

public sealed class DojiOptions
{
  public const string SectionName = "Doji";
  public string BaseUrl { get; set; } = "https://giavang.doji.vn";
  public string PriceEndpoint { get; set; } = "/api/giavang/?api_key=258fbd2a72ce8481089d88c678e9fe4f";
  public string[] Regions { get; set; } = ["Hanoi", "HCMC"];
  public int TimeoutSeconds { get; set; } = 10;
}

