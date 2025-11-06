namespace GoldTracker.Domain.Entities;

public sealed class Source
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public string Name { get; init; } = string.Empty;
  public string BaseUrl { get; init; } = string.Empty;
  public bool Active { get; init; } = true;
}
