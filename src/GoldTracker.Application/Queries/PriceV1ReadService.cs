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
  private readonly TimeZoneInfo _vietnamTimeZone;

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
    _vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
      Environment.GetEnvironmentVariable("TZ") ?? "Asia/Ho_Chi_Minh");
  }

  private DateTimeOffset ConvertToVietnamTime(DateTimeOffset utcTime)
  {
    return TimeZoneInfo.ConvertTime(utcTime, _vietnamTimeZone);
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
      ConvertToVietnamTime(l.EffectiveAt),
      ConvertToVietnamTime(l.CollectedAt)
    )).ToList();

    return new LatestPricesResponse(items);
  }

  public async Task<PriceHistoryResponse> GetHistoryAsync(HistoryQuery query, CancellationToken ct = default)
  {
    var kind = ValidationHelpers.NormalizeKind(query.Kind);
    var brand = query.Brand?.Trim();
    var region = query.Region?.Trim();

    // Resolve product: if filters narrow to 1 product, use it; otherwise query directly
    Product? product = null;
    Source? source = null;

    var formEnum = Enum.TryParse<GoldForm>(kind, true, out var f) ? f : GoldForm.Ring;
    
    if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(region))
    {
      // Try to find specific product first
      product = await _productRepo.FindAsync(brand, formEnum, null, region, ct);
    }

    // If not found, query directly from database (don't fallback to DOJI)
    if (product is null)
    {
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

    if (product is null)
      throw new InvalidOperationException($"Cannot resolve product for kind={kind}, brand={brand}, region={region}");

    // Get source: try multiple variants to match brand to source name
    // For PhucThanh, source name is "PHUC_THANH" (from parser)
    if (product.Brand.Contains("Phuc", StringComparison.OrdinalIgnoreCase))
    {
      // 1. Try "PHUC_THANH" first (actual source name from parser)
      source = await _sourceRepo.GetByNameAsync("PHUC_THANH", ct);
      // 2. Try "PhucThanh" (brand name) - unlikely but try anyway
      if (source is null)
      {
        source = await _sourceRepo.GetByNameAsync("PhucThanh", ct);
      }
      // 3. Try "PHUCTHANH" (uppercase, no underscore)
      if (source is null)
      {
        source = await _sourceRepo.GetByNameAsync("PHUCTHANH", ct);
      }
      // For PhucThanh, DO NOT fallback to DOJI - throw error if source not found
      if (source is null)
      {
        throw new InvalidOperationException($"Source not found for PhucThanh brand. Expected 'PHUC_THANH' but not found in database.");
      }
    }
    else
    {
      // For other brands, try brand name first
      source = await _sourceRepo.GetByNameAsync(product.Brand, ct);
      // Try uppercase
      if (source is null)
      {
        source = await _sourceRepo.GetByNameAsync(product.Brand.ToUpperInvariant(), ct);
      }
      // Backwards compatible fallback: DOJI as default source (only for non-PhucThanh)
      if (source is null)
      {
        source = await _sourceRepo.GetByNameAsync("DOJI", ct);
      }
    }
    
    // Last resort: any source (only if still null and not PhucThanh)
    if (source is null && !product.Brand.Contains("Phuc", StringComparison.OrdinalIgnoreCase))
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

    // Get history from daily_snapshot, fallback to price_tick if no snapshot data
    using var conn2 = _connectionFactory.CreateConnection();
    if (conn2 is System.Data.IDbConnection dbConn2)
      await Task.Run(() => dbConn2.Open(), ct);

    // First try daily_snapshot
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
    // Convert DateOnly to DateTime for Dapper (PostgreSQL DATE type accepts DateTime)
    historyParams.Add("fromDate", fromDate.ToDateTime(TimeOnly.MinValue));
    historyParams.Add("toDate", toDate.ToDateTime(TimeOnly.MinValue));

    var history = await Dapper.SqlMapper.QueryAsync<(DateTime DateUtc, decimal PriceBuyClose, decimal PriceSellClose)>(conn2, historySql, historyParams);

    // If no snapshot data or insufficient data, fallback to price_tick (last tick per day)
    // Use fallback if we have less than 5 days of snapshot data
    if (history.Count() < 5)
    {
      var fallbackSql = @"
        WITH daily_last_ticks AS (
          SELECT DISTINCT ON (gold.fn_local_date(pt.effective_at))
            gold.fn_local_date(pt.effective_at) as Date,
            pt.price_buy as PriceBuyClose,
            pt.price_sell as PriceSellClose
          FROM gold.price_tick pt
          WHERE pt.product_id = @productId
            AND pt.source_id = @sourceId
            AND gold.fn_local_date(pt.effective_at) >= @fromDate
            AND gold.fn_local_date(pt.effective_at) <= @toDate
          ORDER BY gold.fn_local_date(pt.effective_at), pt.effective_at DESC, pt.collected_at DESC, pt.id DESC
        )
        SELECT Date::timestamp as DateUtc, PriceBuyClose, PriceSellClose
        FROM daily_last_ticks
        ORDER BY Date ASC";
      
      var fallbackHistory = await Dapper.SqlMapper.QueryAsync<(DateTime DateUtc, decimal PriceBuyClose, decimal PriceSellClose)>(conn2, fallbackSql, historyParams);
      // Use fallback if it has more data than snapshot
      if (fallbackHistory.Count() > history.Count())
      {
        history = fallbackHistory;
      }
    }

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

  public async Task<PricesByDateResponse> GetByDateAsync(ByDateQuery query, CancellationToken ct = default)
  {
    var kind = ValidationHelpers.NormalizeKind(query.Kind);
    var brand = query.Brand?.Trim();
    var region = query.Region?.Trim();

    // Convert date to timezone-aware range (start and end of day in Vietnam timezone)
    var dateStart = new DateTime(query.Date.Year, query.Date.Month, query.Date.Day, 0, 0, 0);
    var dateEnd = dateStart.AddDays(1).AddTicks(-1);
    
    // Convert to UTC for database query
    var dateStartUtc = TimeZoneInfo.ConvertTimeToUtc(dateStart, _vietnamTimeZone);
    var dateEndUtc = TimeZoneInfo.ConvertTimeToUtc(dateEnd, _vietnamTimeZone);

    using var conn = _connectionFactory.CreateConnection();
    if (conn is System.Data.IDbConnection dbConn)
      await Task.Run(() => dbConn.Open(), ct);

    var sql = @"
      SELECT
        pt.id,
        pt.product_id as ProductId,
        p.brand as Brand,
        p.form as Form,
        p.karat as Karat,
        p.region as Region,
        s.name as SourceName,
        pt.price_buy as PriceBuy,
        pt.price_sell as PriceSell,
        pt.currency as Currency,
        pt.collected_at as CollectedAt,
        pt.effective_at as EffectiveAt
      FROM gold.price_tick pt
      JOIN gold.product p ON p.id = pt.product_id
      JOIN gold.source s ON s.id = pt.source_id
      WHERE pt.effective_at >= @dateStartUtc
        AND pt.effective_at < @dateEndUtc";

    var parameters = new Dapper.DynamicParameters();
    parameters.Add("dateStartUtc", dateStartUtc);
    parameters.Add("dateEndUtc", dateEndUtc);

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

    sql += " ORDER BY pt.effective_at ASC, p.brand, p.form, p.karat, p.region, s.name";

    var ticks = await Dapper.SqlMapper.QueryAsync<(long Id, Guid ProductId, string Brand, string Form, int? Karat, string? Region, string SourceName, decimal PriceBuy, decimal PriceSell, string Currency, DateTimeOffset CollectedAt, DateTimeOffset EffectiveAt)>(conn, sql, parameters);

    var items = ticks.Select(t => new PriceItemDto(
      t.ProductId,
      t.Brand,
      t.Form,
      t.Karat,
      t.Region ?? string.Empty,
      t.SourceName,
      t.PriceBuy,
      t.PriceSell,
      t.Currency,
      ConvertToVietnamTime(t.EffectiveAt),
      ConvertToVietnamTime(t.CollectedAt)
    )).ToList();

    return new PricesByDateResponse(query.Date, items);
  }
}

