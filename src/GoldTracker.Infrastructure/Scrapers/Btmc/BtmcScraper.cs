using System.Net.Http;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Scrapers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldTracker.Infrastructure.Scrapers.Btmc;

public sealed class BtmcScraper : IBtmcScraper
{
  private readonly HttpClient _httpClient;
  private readonly BtmcOptions _options;
  private readonly BtmcParser _parser;
  private readonly IPriceNormalizer _normalizer;
  private readonly IPriceTickRepository _tickRepository;
  private readonly ScraperHealthTracker _healthTracker;
  private readonly ILogger<BtmcScraper> _logger;

  public BtmcScraper(
    IHttpClientFactory httpClientFactory,
    IOptions<BtmcOptions> options,
    BtmcParser parser,
    IPriceNormalizer normalizer,
    IPriceTickRepository tickRepository,
    ScraperHealthTracker healthTracker,
    ILogger<BtmcScraper> logger)
  {
    _httpClient = httpClientFactory.CreateClient("btmc");
    _options = options.Value;
    _parser = parser;
    _normalizer = normalizer;
    _tickRepository = tickRepository;
    _healthTracker = healthTracker;
    _logger = logger;
  }

  public async Task<int> RunOnceAsync(CancellationToken ct = default)
  {
    var anomalies = new List<string>();
    try
    {
      if (string.IsNullOrWhiteSpace(_options.PriceUrl))
        throw new InvalidOperationException("BTMC price URL is not configured");

      _logger.LogInformation("Fetching BTMC prices from {Url}", _options.PriceUrl);

      var response = await ExecuteWithRetryAsync(ct);
      if (string.IsNullOrWhiteSpace(response))
      {
        const string message = "Empty response from BTMC service";
        _logger.LogWarning(message);
        _healthTracker.RecordFailure(message);
        return 0;
      }

      var records = _parser.Parse(response);
      if (records.Count == 0)
      {
        const string message = "No BTMC price records parsed from response";
        _logger.LogWarning(message);
        _healthTracker.RecordFailure(message);
        return 0;
      }

      _logger.LogInformation("Parsed {Count} raw BTMC price records", records.Count);

      var inserted = 0;
      foreach (var raw in records)
      {
        if (IsAnomalous(raw, out var reason))
        {
          anomalies.Add(reason);
          _logger.LogWarning("Skipping BTMC record due to anomaly: {Reason} {@Record}",
            reason, new { raw.Brand, raw.Form, raw.Karat, raw.PriceBuy, raw.PriceSell });
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
          var context = $"{raw.Brand}/{raw.Form}/{raw.Karat}";
          anomalies.Add($"normalize:{context}:{ex.GetType().Name}");
          _logger.LogWarning(ex, "Failed to normalize/insert BTMC price record: {Context}", context);
        }
      }

      var anomalySummary = anomalies.Count == 0 ? null : string.Join(" | ", anomalies.Distinct());
      _healthTracker.RecordSuccess(inserted, anomalies.Count, anomalySummary);
      _logger.LogInformation("Inserted {Count} BTMC price ticks (anomalies: {Anomalies})", inserted, anomalies.Count);
      return inserted;
    }
    catch (Exception ex)
    {
      _healthTracker.RecordFailure(ex);
      _logger.LogError(ex, "Error running BTMC scraper");
      return 0;
    }
  }

  public ScraperHealthSnapshot GetHealth() => _healthTracker.Snapshot();

  private async Task<string?> ExecuteWithRetryAsync(CancellationToken ct)
  {
    var attempts = Math.Max(1, _options.RetryCount);
    Exception? lastError = null;

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
      try
      {
        return await _httpClient.GetStringAsync(_options.PriceUrl, ct);
      }
      catch (HttpRequestException ex) when (attempt < attempts)
      {
        lastError = ex;
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
        _logger.LogWarning(ex, "BTMC request attempt {Attempt}/{Total} failed, retrying in {Delay}s",
          attempt, attempts, delay.TotalSeconds);
        await Task.Delay(delay, ct);
      }
      catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && attempt < attempts)
      {
        lastError = ex;
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
        _logger.LogWarning(ex, "BTMC request timeout on attempt {Attempt}/{Total}, retrying in {Delay}s",
          attempt, attempts, delay.TotalSeconds);
        await Task.Delay(delay, ct);
      }
    }

    if (lastError != null)
      throw lastError;

    return null;
  }

  private bool IsAnomalous(RawPriceRecord record, out string reason)
  {
    reason = string.Empty;
    if (record.PriceBuy is null || record.PriceSell is null)
    {
      reason = "missing price";
      return true;
    }

    if (record.PriceBuy <= 0 || record.PriceSell <= 0)
    {
      reason = "non-positive price";
      return true;
    }

    if (record.PriceSell < record.PriceBuy)
    {
      reason = "sell price below buy price";
      return true;
    }

    if (record.PriceBuy < _options.MinPrice || record.PriceBuy > _options.MaxPrice)
    {
      reason = $"buy price out of bounds ({record.PriceBuy:0})";
      return true;
    }

    var spread = record.PriceSell.Value - record.PriceBuy.Value;
    if (record.PriceBuy > 0)
    {
      var ratio = spread / record.PriceBuy.Value;
      if (ratio > _options.MaxSpreadRatio)
      {
        reason = $"spread ratio {ratio:P} exceeds limit";
        return true;
      }
    }

    return false;
  }
}

