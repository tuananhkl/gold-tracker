using GoldTracker.Application.Contracts;
using GoldTracker.Application.DTOs;

namespace GoldTracker.Application.Services;

public sealed class InMemoryPriceService : IPriceQuery, IChangeQuery
{
  private static readonly DateOnly Day1 = new(2025, 11, 1);
  private static readonly DateOnly Day2 = new(2025, 11, 2);

  public Task<LatestPriceDto> GetLatestAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    var items = new List<LatestPriceDto.Item>
    {
      new()
      {
        ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Brand = brand ?? "DOJI",
        Form = (kind ?? "ring").ToLowerInvariant(),
        Karat = 24,
        Region = region ?? "Hanoi",
        PriceBuy = 7420000,
        PriceSell = 7520000,
        Currency = "VND"
      }
    };
    return Task.FromResult(new LatestPriceDto
    {
      AsOf = new DateTimeOffset(2025, 11, 02, 09, 30, 00, TimeSpan.Zero),
      Items = items
    });
  }

  public Task<(DateOnly from, DateOnly to, IReadOnlyList<HistoryPointDto> points)> GetHistoryAsync(string? kind, int days, string? brand, string? region, CancellationToken ct = default)
  {
    var to = Day2;
    var from = to.AddDays(-(days - 1));
    var points = new List<HistoryPointDto>
    {
      new() { Date = Day1, PriceSell = 7480000 },
      new() { Date = Day2, PriceSell = 7520000 }
    };
    return Task.FromResult((from, to, (IReadOnlyList<HistoryPointDto>)points));
  }

  public Task<DayChangeDto> GetChangesAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    var items = new List<DayChangeDto.Item>
    {
      new()
      {
        Brand = brand ?? "DOJI",
        Form = (kind ?? "ring").ToLowerInvariant(),
        Region = region ?? "Hanoi",
        PriceSellClose = 7520000,
        DeltaVsYesterday = 40000,
        Direction = "up"
      }
    };
    return Task.FromResult(new DayChangeDto { Date = Day2, Items = items });
  }
}
