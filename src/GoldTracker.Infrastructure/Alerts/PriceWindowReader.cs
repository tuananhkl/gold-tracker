using Dapper;
using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Infrastructure.Persistence;

namespace GoldTracker.Infrastructure.Alerts;

public sealed class PriceWindowReader : IPriceWindowReader
{
  private readonly DapperConnectionFactory _factory;

  public PriceWindowReader(DapperConnectionFactory factory)
  {
    _factory = factory;
  }

  public async Task<IReadOnlyList<PricePoint>> GetLastHourSeriesAsync(string? brand, string? region, string? productKind, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);
    var sql = @"
      SELECT
        pt.effective_at as Timestamp,
        pt.price_buy as PriceBuy,
        pt.price_sell as PriceSell
      FROM gold.price_tick pt
      JOIN gold.product p ON p.id = pt.product_id
      WHERE pt.effective_at >= @OneHourAgo";

    var parameters = new DynamicParameters();
    parameters.Add("OneHourAgo", oneHourAgo);

    if (!string.IsNullOrWhiteSpace(brand))
    {
      sql += " AND p.brand = @brand";
      parameters.Add("brand", brand);
    }
    if (!string.IsNullOrWhiteSpace(region))
    {
      sql += " AND p.region = @region";
      parameters.Add("region", region);
    }
    if (!string.IsNullOrWhiteSpace(productKind))
    {
      sql += " AND p.form = @form";
      parameters.Add("form", productKind);
    }

    sql += " ORDER BY pt.effective_at ASC";

    var results = await conn.QueryAsync<PricePoint>(sql, parameters);
    return results.ToList();
  }

  public async Task<PricePoint?> GetLatestSnapshotAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var sql = @"
      SELECT
        v.effective_at as Timestamp,
        v.price_buy as PriceBuy,
        v.price_sell as PriceSell
      FROM gold.v_latest_price_per_product v
      JOIN gold.product p ON p.id = v.product_id
      WHERE 1=1";

    var parameters = new DynamicParameters();
    if (!string.IsNullOrWhiteSpace(kind))
    {
      sql += " AND p.form = @form";
      parameters.Add("form", kind);
    }
    if (!string.IsNullOrWhiteSpace(brand))
    {
      sql += " AND p.brand = @brand";
      parameters.Add("brand", brand);
    }
    if (!string.IsNullOrWhiteSpace(region))
    {
      sql += " AND p.region = @region";
      parameters.Add("region", region);
    }

    sql += " ORDER BY v.effective_at DESC LIMIT 1";

    var result = await conn.QueryFirstOrDefaultAsync<PricePoint>(sql, parameters);
    return result;
  }
}

