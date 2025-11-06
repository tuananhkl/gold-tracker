namespace GoldTracker.Application.DTOs;

public sealed record DayChangeDto
{
  public DateOnly Date { get; init; }
  public IReadOnlyList<Item> Items { get; init; } = Array.Empty<Item>();
  public sealed record Item
  {
    public string Brand { get; init; } = string.Empty;
    public string Form { get; init; } = string.Empty;
    public string? Region { get; init; }
    public decimal PriceSellClose { get; init; }
    public decimal DeltaVsYesterday { get; init; }
    public string Direction { get; init; } = "flat"; // up/down/flat
  }
}
