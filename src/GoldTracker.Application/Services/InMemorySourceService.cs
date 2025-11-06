using GoldTracker.Application.Contracts;
using GoldTracker.Application.DTOs;

namespace GoldTracker.Application.Services;

public sealed class InMemorySourceService : ISourceQuery
{
  public Task<SourceHealthDto> GetHealthAsync(CancellationToken ct = default)
  {
    var sources = new List<SourceHealthDto.Item>
    {
      new() { Name = "DOJI", BaseUrl = "https://doji.vn", Status = "ok" },
      new() { Name = "BTMC", BaseUrl = "https://btmc.vn", Status = "ok" },
      new() { Name = "SJC",  BaseUrl = "https://sjc.com.vn", Status = "ok" },
      new() { Name = "PhucThanh", BaseUrl = "https://vangbacphucthanh.vn", Status = "ok" }
    };
    return Task.FromResult(new SourceHealthDto { Sources = sources });
  }
}
