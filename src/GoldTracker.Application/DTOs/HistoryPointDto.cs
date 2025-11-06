namespace GoldTracker.Application.DTOs;

public sealed record HistoryPointDto
{
  public DateOnly Date { get; init; }
  public decimal PriceSell { get; init; }
}
