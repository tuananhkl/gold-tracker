namespace GoldTracker.Infrastructure.Config;

public sealed class ElasticsearchOptions
{
  public const string SectionName = "Elasticsearch";

  public bool Enabled { get; set; } = true;
  public string BaseUrl { get; set; } = "https://192.168.31.156:9200";
  public string IndexLogs { get; set; } = "gold-tracker-api-application-*";
  public int TimeoutSeconds { get; set; } = 5;
  public string? Username { get; set; }
  public string? Password { get; set; }
  /// <summary>
  /// Dev-only escape hatch for self-signed/partial-chain Elasticsearch TLS.
  /// When true, the HTTP client will skip server certificate validation.
  /// </summary>
  public bool SkipTlsVerify { get; set; } = false;
}

