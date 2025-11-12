using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldTracker.Infrastructure.Alerts;

public sealed class AlertEvaluator : IAlertEvaluator
{
  private readonly IPriceWindowReader _priceReader;
  private readonly IScraperHealthReader _healthReader;
  private readonly AlertsOptions _options;
  private readonly ILogger<AlertEvaluator> _logger;

  public AlertEvaluator(
    IPriceWindowReader priceReader,
    IScraperHealthReader healthReader,
    IOptions<AlertsOptions> options,
    ILogger<AlertEvaluator> logger)
  {
    _priceReader = priceReader;
    _healthReader = healthReader;
    _options = options.Value;
    _logger = logger;
  }

  public async Task<IReadOnlyList<AlertCandidate>> EvaluatePriceJumpAsync(decimal percentThreshold, CancellationToken ct = default)
  {
    var candidates = new List<AlertCandidate>();
    var brands = _options.BrandFilter.Count > 0 ? _options.BrandFilter.ToArray() : new[] { "DOJI", "SJC", "BTMC", "PhucThanh" };
    var regions = new[] { "Hanoi", "HCMC", "Central" };
    var kinds = new[] { "ring", "bar" };

    foreach (var brand in brands)
    {
      foreach (var region in regions)
      {
        foreach (var kind in kinds)
        {
          try
          {
            var series = await _priceReader.GetLastHourSeriesAsync(brand, region, kind, ct);
            if (series.Count < 2) continue;

            var first = series[0];
            var last = series[^1];
            var changePercent = ((last.PriceSell - first.PriceSell) / first.PriceSell) * 100m;

            if (Math.Abs(changePercent) >= percentThreshold)
            {
              var direction = changePercent > 0 ? "↑" : "↓";
              var dedupKey = $"price_jump:{brand}:{region}:{kind}:{DateTimeOffset.UtcNow:yyyyMMddHH}";
              candidates.Add(new AlertCandidate
              {
                Kind = "price_jump",
                Brand = brand,
                Region = region,
                ProductKind = kind,
                Message = $"{direction} {Math.Abs(changePercent):F2}% trong 1h: {brand} {kind} {region} - {first.PriceSell:N0} → {last.PriceSell:N0} VND",
                Severity = Math.Abs(changePercent) >= 5m ? "high" : "warn",
                Meta = new Dictionary<string, object>
                {
                  ["change_percent"] = changePercent,
                  ["first_price"] = first.PriceSell,
                  ["last_price"] = last.PriceSell,
                  ["first_timestamp"] = first.Timestamp,
                  ["last_timestamp"] = last.Timestamp
                },
                DedupKey = dedupKey
              });
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to evaluate price jump for {Brand}/{Region}/{Kind}", brand, region, kind);
          }
        }
      }
    }

    return candidates;
  }

  public async Task<IReadOnlyList<AlertCandidate>> EvaluateNoDataAsync(int noDataMinutes, CancellationToken ct = default)
  {
    var candidates = new List<AlertCandidate>();
    var within = TimeSpan.FromMinutes(noDataMinutes);
    var hasNoData = await _healthReader.HasNoDataAsync(within, ct);

    if (hasNoData)
    {
      var dedupKey = $"no_data:{DateTimeOffset.UtcNow:yyyyMMddHHmm}";
      candidates.Add(new AlertCandidate
      {
        Kind = "no_data",
        Message = $"Không có dữ liệu giá mới trong {noDataMinutes} phút",
        Severity = "warn",
        Meta = new Dictionary<string, object> { ["no_data_minutes"] = noDataMinutes },
        DedupKey = dedupKey
      });
    }

    return candidates;
  }

  public async Task<IReadOnlyList<AlertCandidate>> EvaluateScraperErrorsAsync(CancellationToken ct = default)
  {
    var candidates = new List<AlertCandidate>();
    var within = TimeSpan.FromMinutes(10);
    var hasErrors = await _healthReader.HasErrorsAsync(within, ct);

    if (hasErrors)
    {
      var dedupKey = $"scraper_error:{DateTimeOffset.UtcNow:yyyyMMddHH}";
      candidates.Add(new AlertCandidate
      {
        Kind = "scraper_error",
        Message = "Phát hiện lỗi scraper trong 10 phút gần đây",
        Severity = "high",
        Meta = new Dictionary<string, object> { ["window_minutes"] = 10 },
        DedupKey = dedupKey
      });
    }

    return candidates;
  }
}

