namespace GoldTracker.Application.DTOs;

public sealed record LatestPriceDto
{
  public DateTimeOffset AsOf { get; init; }
  public IReadOnlyList<Item> Items { get; init; } = Array.Empty<Item>();
  public sealed record Item
  {
    public Guid ProductId { get; init; }
    public string Brand { get; init; } = string.Empty;
    public string Form { get; init; } = string.Empty;
    public int? Karat { get; init; }
    public string? Region { get; init; }
    public decimal PriceBuy { get; init; }
    public decimal PriceSell { get; init; }
    public string Currency { get; init; } = "VND";
  }
}
