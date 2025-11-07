using Dapper;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Entities;
using GoldTracker.Infrastructure.Persistence;

namespace GoldTracker.Infrastructure.Persistence.Repositories;

public sealed class SourceRepository : ISourceRepository
{
  private readonly DapperConnectionFactory _factory;

  public SourceRepository(DapperConnectionFactory factory)
  {
    _factory = factory;
  }

  public async Task<Source?> GetByNameAsync(string name, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    var result = await conn.QueryFirstOrDefaultAsync<Source>(
      "SELECT id, name, base_url as BaseUrl, active, created_at as CreatedAt FROM gold.source WHERE name = @name",
      new { name });
    return result;
  }

  public async Task<Source> EnsureAsync(string name, string baseUrl, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    var existing = await GetByNameAsync(name, ct);
    if (existing is not null)
      return existing;

    var id = Guid.NewGuid();
    await conn.ExecuteAsync(
      @"INSERT INTO gold.source (id, name, kind, base_url, active, created_at)
        VALUES (@id, @name, 'retailer', @baseUrl, true, now())
        ON CONFLICT (name) DO UPDATE SET base_url = EXCLUDED.base_url
        RETURNING id, name, base_url as BaseUrl, active, created_at as CreatedAt",
      new { id, name, baseUrl });

    return await GetByNameAsync(name, ct) ?? throw new InvalidOperationException("Failed to create source");
  }
}

