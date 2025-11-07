using System.Net.Http;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Normalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace GoldTracker.Infrastructure.Scrapers.Doji;

public sealed class DojiScraper : IDojiScraper
{
  private readonly HttpClient _httpClient;
  private readonly DojiOptions _options;
  private readonly DojiParser _parser;
  private readonly IPriceNormalizer _normalizer;
  private readonly IPriceTickRepository _tickRepo;
  private readonly ILogger<DojiScraper> _logger;

  public DojiScraper(
    IHttpClientFactory httpClientFactory,
    IOptions<DojiOptions> options,
    DojiParser parser,
    IPriceNormalizer normalizer,
    IPriceTickRepository tickRepo,
    ILogger<DojiScraper> logger)
  {
    _httpClient = httpClientFactory.CreateClient("doji");
    _options = options.Value;
    _parser = parser;
    _normalizer = normalizer;
    _tickRepo = tickRepo;
    _logger = logger;
  }

  public async Task<int> RunOnceAsync(CancellationToken ct = default)
  {
    try
    {
      var url = $"{_options.BaseUrl.TrimEnd('/')}/{_options.PriceEndpoint.TrimStart('/')}";
      _logger.LogInformation("Fetching DOJI prices from {Url}", url);

      // Retry logic: 3 attempts with exponential backoff
      string? response = null;
      for (int attempt = 1; attempt <= 3; attempt++)
      {
        try
        {
          response = await _httpClient.GetStringAsync(url, ct);
          break;
        }
        catch (HttpRequestException ex) when (attempt < 3)
        {
          var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
          _logger.LogWarning(ex, "Attempt {Attempt} failed, retrying in {Delay}s", attempt, delay.TotalSeconds);
          await Task.Delay(delay, ct);
        }
      }

      if (response == null)
        throw new HttpRequestException("Failed to fetch DOJI prices after 3 attempts");
      
      // Try XML (official API) first, then HTML/JSON fallbacks
      var records = _parser.ParseXml(response, "DOJI");
      if (records.Count == 0)
        records = _parser.ParseHtml(response, "DOJI");
      if (records.Count == 0)
        records = _parser.ParseJson(response, "DOJI");

      if (records.Count == 0)
      {
        _logger.LogWarning("No price records extracted from DOJI response");
        return 0;
      }

      _logger.LogInformation("Parsed {Count} price records from DOJI", records.Count);

      var inserted = 0;
      foreach (var raw in records)
      {
        try
        {
          var (_, _, tick) = await _normalizer.NormalizeAsync(raw, ct);
          await _tickRepo.InsertAsync(tick, ct);
          inserted++;
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to normalize/insert price record: {Brand}, {Form}, {Region}", 
            raw.Brand, raw.Form, raw.Region);
        }
      }

      _logger.LogInformation("Inserted {Count} price ticks from DOJI", inserted);
      return inserted;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error running DOJI scraper");
      return 0;
    }
  }
}

