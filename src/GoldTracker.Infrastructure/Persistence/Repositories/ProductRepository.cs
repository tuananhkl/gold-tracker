using Dapper;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Entities;
using GoldTracker.Domain.Enums;
using GoldTracker.Infrastructure.Persistence;

namespace GoldTracker.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
  private readonly DapperConnectionFactory _factory;

  public ProductRepository(DapperConnectionFactory factory)
  {
    _factory = factory;
  }

  public async Task<Product?> FindAsync(string brand, GoldForm form, int? karat, string? region, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    var result = await conn.QueryFirstOrDefaultAsync<Product>(
      @"SELECT id, brand, form, karat, region, sku_hint as SkuHint, active
        FROM gold.product
        WHERE brand = @brand AND form = @form::text
          AND COALESCE(karat, -1) = COALESCE(@karat, -1)
          AND COALESCE(region, '') = COALESCE(@region, '')",
      new { brand, form = form.ToString(), karat, region });
    return result;
  }

  public async Task<Product> FindOrCreateAsync(string brand, GoldForm form, int? karat, string? region, CancellationToken ct = default)
  {
    var existing = await FindAsync(brand, form, karat, region, ct);
    if (existing is not null)
      return existing;

    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    var id = Guid.NewGuid();
    var result = await conn.QueryFirstOrDefaultAsync<Product>(
      @"INSERT INTO gold.product (id, brand, form, karat, region, active)
        VALUES (@id, @brand, @form::text, @karat, @region, true)
        ON CONFLICT DO NOTHING
        RETURNING id, brand, form, karat, region, sku_hint as SkuHint, active",
      new { id, brand, form = form.ToString(), karat, region });

    if (result is not null)
      return result;

    // Retry find in case of concurrent insert
    return await FindAsync(brand, form, karat, region, ct)
      ?? throw new InvalidOperationException("Failed to create product");
  }
}

