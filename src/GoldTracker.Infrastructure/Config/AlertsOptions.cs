namespace GoldTracker.Infrastructure.Config;

public sealed class AlertsOptions
{
  public const string SectionName = "Alerts";

  public bool Enabled { get; set; } = true;
  public decimal PriceJumpPercent { get; set; } = 2.0m;
  public int NoDataMinutes { get; set; } = 20;
  public string DailyBriefSchedule { get; set; } = "0 8 * * *";
  public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
  public List<string> BrandFilter { get; set; } = new();
}

