namespace GoldTracker.Infrastructure.Scrapers.Btmc;

public sealed class BtmcOptions
{
  public const string SectionName = "Btmc";

  /// <summary>
  /// Full URL to the BTMC price endpoint.
  /// </summary>
  public string PriceUrl { get; set; } = "http://api.btmc.vn/api/BTMCAPI/getpricebtmc?key=3kd8ub1llcg9t45hnoh8hmn7t5kc2v";

  /// <summary>
  /// Http client timeout in seconds.
  /// </summary>
  public int TimeoutSeconds { get; set; } = 10;

  /// <summary>
  /// Total retry attempts (including the first request).
  /// </summary>
  public int RetryCount { get; set; } = 3;

  /// <summary>
  /// Maximum allowed spread (sell - buy) relative to buy price.
  /// </summary>
  public decimal MaxSpreadRatio { get; set; } = 0.05m; // 5%

  /// <summary>
  /// Minimum allowed buy price (VND) to consider a data point valid.
  /// </summary>
  public decimal MinPrice { get; set; } = 9_000_000m;

  /// <summary>
  /// Maximum allowed buy price (VND) to consider a data point valid.
  /// </summary>
  public decimal MaxPrice { get; set; } = 80_000_000m;
}

