using System.Net.Http;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Scrapers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldTracker.Infrastructure.Scrapers.Sjc;

public sealed class SjcScraper : ISjcScraper
{
  private readonly HttpClient _httpClient;
  private readonly SjcOptions _options;
  private readonly SjcParser _parser;
  private readonly IPriceNormalizer _normalizer;
  private readonly IPriceTickRepository _tickRepo;
  private readonly ScraperHealthTracker _health;
  private readonly ILogger<SjcScraper> _logger;

  public SjcScraper(
    IHttpClientFactory httpClientFactory,
    IOptions<SjcOptions> options,
    SjcParser parser,
    IPriceNormalizer normalizer,
    IPriceTickRepository tickRepo,
    ScraperHealthTracker health,
    ILogger<SjcScraper> logger)
  {
    _httpClient = httpClientFactory.CreateClient("sjc");
    _options = options.Value;
    _parser = parser;
    _normalizer = normalizer;
    _tickRepo = tickRepo;
    _health = health;
    _logger = logger;
  }

  public async Task<int> RunOnceAsync(CancellationToken ct = default)
  {
    var anomalies = new List<string>();
    try
    {
      _logger.LogInformation("Fetching SJC prices from {Url}", _options.PriceUrl);
      var payload = await ExecuteWithRetryAsync(ct);
      if (string.IsNullOrWhiteSpace(payload))
      {
        const string msg = "Empty response from SJC service";
        _logger.LogWarning(msg);
        _health.RecordFailure(msg);
        return 0;
      }

      var records = _parser.Parse(payload);
      if (records.Count == 0)
      {
        const string msg = "No SJC price records parsed from response";
        _logger.LogWarning(msg);
        _health.RecordFailure(msg);
        return 0;
      }

      _logger.LogInformation("Parsed {Count} raw SJC price records", records.Count);

      var inserted = 0;
      foreach (var raw in records)
      {
        if (IsAnomalous(raw, out var reason))
        {
          anomalies.Add(reason);
          _logger.LogWarning("Skipping SJC record due to anomaly: {Reason} {@Record}",
            reason, new { raw.Brand, raw.Form, raw.Karat, raw.Region, raw.PriceBuy, raw.PriceSell });
          continue;
        }

        try
        {
          var (_, _, tick) = await _normalizer.NormalizeAsync(raw, ct);
          await _tickRepo.InsertAsync(tick, ct);
          inserted++;
        }
        catch (Exception ex)
        {
          var ctx = $"{raw.Brand}/{raw.Form}/{raw.Karat}/{raw.Region}";
          anomalies.Add($"normalize:{ctx}:{ex.GetType().Name}");
          _logger.LogWarning(ex, "Failed to normalize/insert SJC record: {Context}", ctx);
        }
      }

      var summary = anomalies.Count == 0 ? null : string.Join(" | ", anomalies.Distinct());
      _health.RecordSuccess(inserted, anomalies.Count, summary);
      _logger.LogInformation("Inserted {Count} SJC ticks (anomalies: {Anomalies})", inserted, anomalies.Count);
      return inserted;
    }
    catch (Exception ex)
    {
      _health.RecordFailure(ex);
      _logger.LogError(ex, "Error running SJC scraper");
      return 0;
    }
  }

  public ScraperHealthSnapshot GetHealth() => _health.Snapshot();

  private async Task<string?> ExecuteWithRetryAsync(CancellationToken ct)
  {
    var attempts = Math.Max(1, _options.RetryCount);
    Exception? last = null;
    for (var attempt = 1; attempt <= attempts; attempt++)
    {
      try
      {
        return await _httpClient.GetStringAsync(_options.PriceUrl, ct);
      }
      catch (HttpRequestException ex) when (attempt < attempts)
      {
        last = ex;
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
        _logger.LogWarning(ex, "SJC attempt {Attempt}/{Total} failed; retry in {Delay}s", attempt, attempts, delay.TotalSeconds);
        await Task.Delay(delay, ct);
      }
      catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && attempt < attempts)
      {
        last = ex;
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
        _logger.LogWarning(ex, "SJC timeout attempt {Attempt}/{Total}; retry in {Delay}s", attempt, attempts, delay.TotalSeconds);
        await Task.Delay(delay, ct);
      }
    }
    if (last != null) throw last;
    return null;
  }

  private bool IsAnomalous(RawPriceRecord r, out string reason)
  {
    reason = string.Empty;
    if (r.PriceBuy is null || r.PriceSell is null) { reason = "missing price"; return true; }
    if (r.PriceBuy <= 0 || r.PriceSell <= 0) { reason = "non-positive price"; return true; }
    if (r.PriceSell < r.PriceBuy) { reason = "sell below buy"; return true; }
    if (r.PriceBuy < _options.MinPrice || r.PriceBuy > _options.MaxPrice) { reason = "price out of bounds"; return true; }
    var spread = r.PriceSell.Value - r.PriceBuy.Value;
    var ratio = r.PriceBuy.Value > 0 ? spread / r.PriceBuy.Value : 0;
    if (ratio > _options.MaxSpreadRatio) { reason = $"spread {ratio:P} too high"; return true; }
    return false;
  }
}

