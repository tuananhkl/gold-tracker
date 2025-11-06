namespace GoldTracker.Application.DTOs;

public sealed record SourceHealthDto
{
  public IReadOnlyList<Item> Sources { get; init; } = Array.Empty<Item>();
  public sealed record Item
  {
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Status { get; init; } = "ok";
  }
}
