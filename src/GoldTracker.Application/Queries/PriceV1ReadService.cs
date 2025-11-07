using Dapper;
using GoldTracker.Application.Contracts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Entities;
using GoldTracker.Domain.Enums;

namespace GoldTracker.Application.Queries;

public sealed class PriceV1ReadService : IPriceV1Query
{
  private readonly IPriceTickRepository _tickRepo;
  private readonly IProductRepository _productRepo;
  private readonly ISourceRepository _sourceRepo;
  private readonly IDbConnectionFactory _connectionFactory;

  public PriceV1ReadService(
    IPriceTickRepository tickRepo,
    IProductRepository productRepo,
    ISourceRepository sourceRepo,
    IDbConnectionFactory connectionFactory)
  {
    _tickRepo = tickRepo;
    _productRepo = productRepo;
    _sourceRepo = sourceRepo;
    _connectionFactory = connectionFactory;
  }

  public async Task<LatestPricesResponse> GetLatestAsync(LatestQuery query, CancellationToken ct = default)
  {
    var kind = ValidationHelpers.NormalizeKind(query.Kind);
    var brand = query.Brand?.Trim();
    var region = query.Region?.Trim();

    // Query directly with joins to get full details
    using var conn = _connectionFactory.CreateConnection();
    if (conn is System.Data.IDbConnection dbConn)
      await Task.Run(() => dbConn.Open(), ct);

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

    var parameters = new Dapper.DynamicParameters();
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

    var latest = await Dapper.SqlMapper.QueryAsync<(Guid ProductId, string Brand, string Form, int? Karat, string? Region, string SourceName, decimal PriceBuy, decimal PriceSell, string Currency, DateTimeOffset CollectedAt, DateTimeOffset EffectiveAt)>(conn, sql, parameters);

    var items = latest.Select(l => new PriceItemDto(
      l.ProductId,
      l.Brand,
      l.Form,
      l.Karat,
      l.Region ?? string.Empty,
      l.SourceName,
      l.PriceBuy,
      l.PriceSell,
      l.Currency,
      l.EffectiveAt,
      l.CollectedAt
    )).ToList();

    return new LatestPricesResponse(items);
  }

  public async Task<PriceHistoryResponse> GetHistoryAsync(HistoryQuery query, CancellationToken ct = default)
  {
    var kind = ValidationHelpers.NormalizeKind(query.Kind);
    var brand = query.Brand?.Trim();
    var region = query.Region?.Trim();

    // Resolve product: if filters narrow to 1 product, use it; otherwise default to ring + DOJI + first matching
    Product? product = null;
    Source? source = null;

    if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(region))
    {
      // Try to find specific product
      var formEnum = Enum.TryParse<GoldForm>(kind, true, out var f) ? f : GoldForm.Ring;
      product = await _productRepo.FindAsync(brand, formEnum, null, region, ct);
    }

    // If not found, default to ring + DOJI + first matching
    if (product is null)
    {
      var formEnum = Enum.TryParse<GoldForm>(kind, true, out var f) ? f : GoldForm.Ring;
      product = await _productRepo.FindAsync("DOJI", formEnum, 24, "Hanoi", ct);
      
      // If still not found, try to find any ring product
      if (product is null)
      {
        // Query for first matching product
        using var conn = _connectionFactory.CreateConnection();
        if (conn is System.Data.IDbConnection dbConn)
          await Task.Run(() => dbConn.Open(), ct);
        var sql = @"
          SELECT id, brand, form, karat, region, sku_hint as SkuHint, active
          FROM gold.product
          WHERE form = @form::text";
        var parameters = new Dapper.DynamicParameters();
        parameters.Add("form", kind);
        
        if (!string.IsNullOrWhiteSpace(brand))
        {
          sql += " AND brand = @brand";
          parameters.Add("brand", brand);
        }
        if (!string.IsNullOrWhiteSpace(region))
        {
          sql += " AND region = @region";
          parameters.Add("region", region);
        }
        
        sql += " ORDER BY brand, karat NULLS LAST, region NULLS LAST LIMIT 1";
        product = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<Product>(conn, sql, parameters) ?? null;
      }
    }

    if (product is null)
      throw new InvalidOperationException($"Cannot resolve product for kind={kind}, brand={brand}, region={region}");

    // Get source (prefer DOJI, otherwise first available)
    source = await _sourceRepo.GetByNameAsync("DOJI", ct);
    if (source is null)
    {
      using var conn = _connectionFactory.CreateConnection();
      if (conn is System.Data.IDbConnection dbConn)
        await Task.Run(() => dbConn.Open(), ct);
      source = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<Source>(conn,
        "SELECT id, name, base_url as BaseUrl, active, created_at as CreatedAt FROM gold.source ORDER BY name LIMIT 1");
    }

    if (source is null)
      throw new InvalidOperationException("No source found in database");

    // Calculate date range
    DateOnly fromDate, toDate;
    if (query.Days.HasValue)
    {
      toDate = DateOnly.FromDateTime(DateTime.UtcNow);
      fromDate = toDate.AddDays(-(query.Days.Value - 1));
    }
    else if (query.From.HasValue && query.To.HasValue)
    {
      fromDate = query.From.Value;
      toDate = query.To.Value;
    }
    else
    {
      // Default 30 days
      toDate = DateOnly.FromDateTime(DateTime.UtcNow);
      fromDate = toDate.AddDays(-29);
    }

    // Get history from daily_snapshot
    using var conn2 = _connectionFactory.CreateConnection();
    if (conn2 is System.Data.IDbConnection dbConn2)
      await Task.Run(() => dbConn2.Open(), ct);

    var historySql = @"
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

    var historyParams = new Dapper.DynamicParameters();
    historyParams.Add("productId", product.Id);
    historyParams.Add("sourceId", source.Id);
    historyParams.Add("fromDate", fromDate);
    historyParams.Add("toDate", toDate);

    var history = await Dapper.SqlMapper.QueryAsync<(DateTime DateUtc, decimal PriceBuyClose, decimal PriceSellClose)>(conn2, historySql, historyParams);

    var points = history.Select(h => new PriceHistoryPointDto(
      DateOnly.FromDateTime(h.DateUtc),
      h.PriceBuyClose,
      h.PriceSellClose
    )).ToList();

    return new PriceHistoryResponse(
      product.Id,
      product.Brand,
      product.Form.ToString(),
      product.Karat,
      product.Region ?? string.Empty,
      source.Name,
      points
    );
  }

  public async Task<PriceChangesResponse> GetChangesAsync(ChangesQuery query, CancellationToken ct = default)
  {
    var kind = ValidationHelpers.NormalizeKind(query.Kind);
    var brand = query.Brand?.Trim();
    var region = query.Region?.Trim();

    using var conn = _connectionFactory.CreateConnection();
    if (conn is System.Data.IDbConnection dbConn)
      await Task.Run(() => dbConn.Open(), ct);

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

    var parameters = new Dapper.DynamicParameters();
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

    var changes = await Dapper.SqlMapper.QueryAsync<(Guid ProductId, string Brand, string Form, int? Karat, string? Region, string SourceName, DateTime DateUtc, decimal PriceSellClose, decimal DeltaVsYesterday, string Direction)>(conn, sql, parameters);

    var items = changes.Select(c => new ChangeItemDto(
      c.ProductId,
      c.Brand,
      c.Form,
      c.Karat,
      c.Region ?? string.Empty,
      c.SourceName,
      DateOnly.FromDateTime(c.DateUtc),
      c.PriceSellClose,
      c.DeltaVsYesterday,
      c.Direction
    )).ToList();

    return new PriceChangesResponse(items);
  }
}

