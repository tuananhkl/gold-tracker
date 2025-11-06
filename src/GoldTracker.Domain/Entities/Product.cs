namespace GoldTracker.Domain.Entities;

using GoldTracker.Domain.Enums;

public sealed class Product
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public string Brand { get; init; } = string.Empty;
  public GoldForm Form { get; init; }
  public int? Karat { get; init; }
  public string? Region { get; init; }
}
