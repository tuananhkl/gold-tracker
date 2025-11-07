using GoldTracker.Application.Contracts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Application.DTOs;

namespace GoldTracker.Application.Queries;

public sealed class PriceReadService : IPriceQuery, IChangeQuery
{
  private readonly IPriceTickRepository _tickRepo;
  private readonly IProductRepository _productRepo;

  public PriceReadService(IPriceTickRepository tickRepo, IProductRepository productRepo)
  {
    _tickRepo = tickRepo;
    _productRepo = productRepo;
  }

  public async Task<LatestPriceDto> GetLatestAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    var ticks = await _tickRepo.GetLatestAsync(kind, brand, region, ct);
    
    var items = new List<LatestPriceDto.Item>();
    foreach (var tick in ticks)
    {
      // Note: We'd need product info in the repository result, but for now use minimal info
      items.Add(new LatestPriceDto.Item
      {
        ProductId = tick.ProductId,
        PriceBuy = tick.PriceBuy,
        PriceSell = tick.PriceSell,
        Currency = tick.Currency
      });
    }

    return new LatestPriceDto
    {
      AsOf = DateTimeOffset.UtcNow,
      Items = items
    };
  }

  public async Task<(DateOnly from, DateOnly to, IReadOnlyList<HistoryPointDto> points)> GetHistoryAsync(string? kind, int days, string? brand, string? region, CancellationToken ct = default)
  {
    var history = await _tickRepo.GetHistoryAsync(kind, days, brand, region, ct);
    var to = DateOnly.FromDateTime(DateTime.UtcNow);
    var from = to.AddDays(-(days - 1));
    var points = history.Select(h => new HistoryPointDto { Date = h.Date, PriceSell = h.PriceSell }).ToList();
    return (from, to, points);
  }

  public async Task<DayChangeDto> GetChangesAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    var changes = await _tickRepo.GetDayOverDayAsync(kind, brand, region, ct);
    var latest = changes.FirstOrDefault();
    if (latest == default)
      return new DayChangeDto { Date = DateOnly.FromDateTime(DateTime.UtcNow), Items = Array.Empty<DayChangeDto.Item>() };

    var items = changes.Select(c => new DayChangeDto.Item
    {
      PriceSellClose = c.PriceSellClose,
      DeltaVsYesterday = c.DeltaVsYesterday,
      Direction = c.Direction
    }).ToList();

    return new DayChangeDto { Date = latest.Date, Items = items };
  }
}

