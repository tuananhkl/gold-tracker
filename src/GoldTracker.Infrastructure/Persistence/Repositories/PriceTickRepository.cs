using Dapper;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Enums;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Persistence;

namespace GoldTracker.Infrastructure.Persistence.Repositories;

public sealed class PriceTickRepository : IPriceTickRepository
{
  private readonly DapperConnectionFactory _factory;

  public PriceTickRepository(DapperConnectionFactory factory)
  {
    _factory = factory;
  }

  public async Task InsertAsync(CanonicalPriceTick tick, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    await conn.ExecuteAsync(
      @"INSERT INTO gold.price_tick (product_id, source_id, price_buy, price_sell, currency, collected_at, effective_at, raw_hash)
        VALUES (@ProductId, @SourceId, @PriceBuy, @PriceSell, @Currency, @CollectedAt, @EffectiveAt, @RawHash)
        ON CONFLICT (product_id, source_id, effective_at)
        DO UPDATE SET
          price_buy = EXCLUDED.price_buy,
          price_sell = EXCLUDED.price_sell,
          currency = EXCLUDED.currency,
          collected_at = EXCLUDED.collected_at,
          raw_hash = EXCLUDED.raw_hash",
      new
      {
        tick.ProductId,
        tick.SourceId,
        tick.PriceBuy,
        tick.PriceSell,
        tick.Currency,
        tick.CollectedAt,
        tick.EffectiveAt,
        tick.RawHash
      });
  }

  public async Task<IReadOnlyList<CanonicalPriceTick>> GetLatestAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var sql = @"
      SELECT
        v.product_id as ProductId,
        v.source_id as SourceId,
        v.price_buy as PriceBuy,
        v.price_sell as PriceSell,
        v.currency as Currency,
        v.collected_at as CollectedAt,
        v.effective_at as EffectiveAt,
        COALESCE(v.raw_hash, '') as RawHash
      FROM gold.v_latest_price_per_product v
      JOIN gold.product p ON p.id = v.product_id
      WHERE 1=1";

    var parameters = new DynamicParameters();
    if (!string.IsNullOrWhiteSpace(kind))
    {
      sql += " AND p.form = @form::text";
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

    var results = await conn.QueryAsync<CanonicalPriceTick>(sql, parameters);
    return results.ToList();
  }

  public async Task<IReadOnlyList<LatestPriceWithDetails>> GetLatestWithDetailsAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var sql = @"
      SELECT
        v.product_id as ProductId,
        p.brand as Brand,
        p.form as Form,
        p.karat as Karat,
        p.region as Region,
        s.name as SourceName,
        v.price_buy as PriceBuy,
        v.price_sell as PriceSell,
        v.currency as Currency,
        v.collected_at as CollectedAt,
        v.effective_at as EffectiveAt
      FROM gold.v_latest_price_per_product v
      JOIN gold.product p ON p.id = v.product_id
      JOIN gold.source s ON s.id = v.source_id
      WHERE 1=1";

    var parameters = new DynamicParameters();
    if (!string.IsNullOrWhiteSpace(kind))
    {
      sql += " AND p.form = @form::text";
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

    var results = await conn.QueryAsync<LatestPriceWithDetails>(sql, parameters);
    return results.ToList();
  }

  public async Task<IReadOnlyList<HistoryPointWithDetails>> GetHistoryWithDetailsAsync(
    Guid productId,
    Guid sourceId,
    DateOnly fromDate,
    DateOnly toDate,
    CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var sql = @"
      SELECT
        ds.date as Date,
        ds.price_buy_close as PriceBuyClose,
        ds.price_sell_close as PriceSellClose
      FROM gold.daily_snapshot ds
      WHERE ds.product_id = @productId
        AND ds.source_id = @sourceId
        AND ds.date >= @fromDate
        AND ds.date <= @toDate
      ORDER BY ds.date ASC";

    var parameters = new DynamicParameters();
    parameters.Add("productId", productId);
    parameters.Add("sourceId", sourceId);
    // Convert DateOnly to DateTime for Dapper (PostgreSQL DATE type accepts DateTime)
    parameters.Add("fromDate", fromDate.ToDateTime(TimeOnly.MinValue));
    parameters.Add("toDate", toDate.ToDateTime(TimeOnly.MinValue));

    var results = await conn.QueryAsync<HistoryPointWithDetails>(sql, parameters);
    return results.ToList();
  }

  public async Task<IReadOnlyList<ChangeItemWithDetails>> GetChangesWithDetailsAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var sql = @"
      SELECT
        v.product_id as ProductId,
        p.brand as Brand,
        p.form as Form,
        p.karat as Karat,
        p.region as Region,
        s.name as SourceName,
        v.date as Date,
        v.price_sell_close as PriceSellClose,
        v.delta_vs_yesterday as DeltaVsYesterday,
        v.direction as Direction
      FROM gold.v_day_over_day v
      JOIN gold.product p ON p.id = v.product_id
      JOIN gold.source s ON s.id = v.source_id
      WHERE 1=1";

    var parameters = new DynamicParameters();
    if (!string.IsNullOrWhiteSpace(kind))
    {
      sql += " AND p.form = @form::text";
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

    sql += @"
      AND v.date = (
        SELECT MAX(v2.date)
        FROM gold.v_day_over_day v2
        JOIN gold.product p2 ON p2.id = v2.product_id
        WHERE 1=1";
    
    if (!string.IsNullOrWhiteSpace(kind))
      sql += " AND p2.form = @form::text";
    if (!string.IsNullOrWhiteSpace(brand))
      sql += " AND p2.brand = @brand";
    if (!string.IsNullOrWhiteSpace(region))
      sql += " AND p2.region = @region";
    
    sql += ") ORDER BY p.brand, p.form, p.karat, p.region, s.name";

    var results = await conn.QueryAsync<ChangeItemWithDetails>(sql, parameters);
    return results.ToList();
  }

  // Helper DTOs for repository results
  public record LatestPriceWithDetails(
    Guid ProductId,
    string Brand,
    string Form,
    int? Karat,
    string? Region,
    string SourceName,
    decimal PriceBuy,
    decimal PriceSell,
    string Currency,
    DateTimeOffset CollectedAt,
    DateTimeOffset EffectiveAt
  );

  public record HistoryPointWithDetails(
    DateOnly Date,
    decimal PriceBuyClose,
    decimal PriceSellClose
  );

  public record ChangeItemWithDetails(
    Guid ProductId,
    string Brand,
    string Form,
    int? Karat,
    string? Region,
    string SourceName,
    DateOnly Date,
    decimal PriceSellClose,
    decimal DeltaVsYesterday,
    string Direction
  );

  public async Task<IReadOnlyList<(DateOnly Date, decimal PriceSell)>> GetHistoryAsync(string? kind, int days, string? brand, string? region, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
    var sql = @"
      SELECT DISTINCT
        gold.fn_local_date(pt.effective_at) as Date,
        pt.price_sell as PriceSell
      FROM gold.price_tick pt
      JOIN gold.product p ON p.id = pt.product_id
      WHERE pt.effective_at >= @fromDate
        AND gold.fn_local_date(pt.effective_at) <= CURRENT_DATE";

    var parameters = new DynamicParameters();
    parameters.Add("fromDate", fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    if (!string.IsNullOrWhiteSpace(kind))
    {
      sql += " AND p.form = @form::text";
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

    sql += " ORDER BY Date ASC";

    var results = await conn.QueryAsync<(DateOnly, decimal)>(sql, parameters);
    return results.ToList();
  }

  public async Task<IReadOnlyList<(DateOnly Date, decimal PriceSellClose, decimal DeltaVsYesterday, string Direction)>> GetDayOverDayAsync(string? kind, string? brand, string? region, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var sql = @"
      SELECT
        v.date as Date,
        v.price_sell_close as PriceSellClose,
        v.delta_vs_yesterday as DeltaVsYesterday,
        v.direction as Direction
      FROM gold.v_day_over_day v
      JOIN gold.product p ON p.id = v.product_id
      WHERE 1=1";

    var parameters = new DynamicParameters();
    if (!string.IsNullOrWhiteSpace(kind))
    {
      sql += " AND p.form = @form::text";
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

    sql += " ORDER BY v.date DESC";

    var results = await conn.QueryAsync<(DateOnly, decimal, decimal, string)>(sql, parameters);
    return results.ToList();
  }
}

