namespace GoldTracker.Infrastructure.Config;

public sealed class DbOptions
{
  public const string SectionName = "ConnectionStrings";
  public string Postgres { get; init; } = string.Empty;
}

