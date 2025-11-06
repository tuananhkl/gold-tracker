namespace GoldTracker.Infrastructure.Config;

public sealed class SourceOptions
{
  public const string SectionName = "Sources";
  public string? DojiApiKey { get; init; }
  public string? BtmcApiKey { get; init; }
  public string? SjcApiKey { get; init; }
}
