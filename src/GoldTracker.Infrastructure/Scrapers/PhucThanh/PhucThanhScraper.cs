using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Normalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldTracker.Infrastructure.Scrapers.PhucThanh;

public sealed class PhucThanhScraper : IPhucThanhScraper
{
  private readonly HttpClient _httpClient;
  private readonly PhucThanhOptions _options;
  private readonly PhucThanhParser _parser;
  private readonly IPriceNormalizer _normalizer;
  private readonly IPriceTickRepository _tickRepository;
  private readonly ScraperHealthTracker _health;
  private readonly ILogger<PhucThanhScraper> _logger;

  public PhucThanhScraper(
    IHttpClientFactory httpClientFactory,
    IOptions<PhucThanhOptions> options,
    PhucThanhParser parser,
    IPriceNormalizer normalizer,
    IPriceTickRepository tickRepository,
    ScraperHealthTracker health,
    ILogger<PhucThanhScraper> logger)
  {
    _httpClient = httpClientFactory.CreateClient("phucthanh");
    _options = options.Value;
    _parser = parser;
    _normalizer = normalizer;
    _tickRepository = tickRepository;
    _health = health;
    _logger = logger;
  }

  public async Task<int> RunOnceAsync(CancellationToken ct = default)
  {
    var anomalies = new List<string>();
    try
    {
      var html = await ExecuteWithRetryAsync(ct);
      if (string.IsNullOrWhiteSpace(html))
      {
        const string msg = "Empty response from PhucThanh site";
        _logger.LogWarning(msg);
        _health.RecordFailure(msg);
        return 0;
      }

      var records = _parser.Parse(html);
      if (records.Count == 0)
      {
        const string msg = "No PhucThanh price rows parsed";
        _logger.LogWarning(msg);
        _health.RecordFailure(msg);
        return 0;
      }

      var inserted = 0;
      foreach (var raw in records)
      {
        if (IsAnomalous(raw, out var reason))
        {
          anomalies.Add(reason);
          _logger.LogWarning("Skip PhucThanh record: {Reason} {@Record}", reason, new { raw.Form, raw.Karat, raw.PriceBuy, raw.PriceSell });
          continue;
        }

        try
        {
          var (_, _, tick) = await _normalizer.NormalizeAsync(raw, ct);
          await _tickRepository.InsertAsync(tick, ct);
          inserted++;
        }
        catch (Exception ex)
        {
          anomalies.Add($"normalize:{ex.GetType().Name}");
          _logger.LogWarning(ex, "Failed to normalize/insert PhucThanh record");
        }
      }

      var anomalySummary = anomalies.Count == 0 ? null : string.Join(" | ", anomalies.Distinct());
      _health.RecordSuccess(inserted, anomalies.Count, anomalySummary);
      _logger.LogInformation("Inserted {Count} PhucThanh ticks (anomalies: {Anomalies})", inserted, anomalies.Count);
      return inserted;
    }
    catch (Exception ex)
    {
      _health.RecordFailure(ex);
      _logger.LogError(ex, "Error running PhucThanh scraper");
      return 0;
    }
  }

  public ScraperHealthSnapshot GetHealth() => _health.Snapshot();

  private async Task<string?> ExecuteWithRetryAsync(CancellationToken ct)
  {
    var attempts = Math.Max(1, _options.RetryCount);
    Exception? last = null;
    for (var i = 1; i <= attempts; i++)
    {
      try
      {
        return await _httpClient.GetStringAsync(_options.BaseUrl, ct);
      }
      catch (Exception ex) when (i < attempts && (ex is HttpRequestException || ex is TaskCanceledException))
      {
        last = ex;
        var delay = TimeSpan.FromSeconds(Math.Pow(2, i - 1));
        _logger.LogWarning(ex, "PhucThanh attempt {Attempt}/{Total} failed; retry in {Delay}s", i, attempts, delay.TotalSeconds);
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
    if (r.PriceSell < r.PriceBuy) { reason = "sell < buy"; return true; }
    if (r.PriceBuy < _options.MinPrice || r.PriceBuy > _options.MaxPrice) { reason = "price out of bounds"; return true; }
    var ratio = (r.PriceSell.Value - r.PriceBuy.Value) / r.PriceBuy.Value;
    if (ratio > _options.MaxSpreadRatio) { reason = $"spread {ratio:P} too high"; return true; }
    return false;
  }
}


