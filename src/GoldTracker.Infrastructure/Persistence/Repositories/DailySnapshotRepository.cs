using Dapper;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Infrastructure.Persistence;

namespace GoldTracker.Infrastructure.Persistence.Repositories;

public sealed class DailySnapshotRepository : IDailySnapshotRepository
{
  private readonly DapperConnectionFactory _factory;

  public DailySnapshotRepository(DapperConnectionFactory factory)
  {
    _factory = factory;
  }

  public async Task UpsertDailyCloseAsync(DateOnly localDate, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    // Get last tick per (product, source) for the given local date
    var sql = @"
      WITH last_ticks AS (
        SELECT DISTINCT ON (pt.product_id, pt.source_id)
          pt.product_id,
          pt.source_id,
          pt.price_buy,
          pt.price_sell
        FROM gold.price_tick pt
        WHERE gold.fn_local_date(pt.effective_at) = @localDate
        ORDER BY pt.product_id, pt.source_id, pt.effective_at DESC, pt.collected_at DESC, pt.id DESC
      )
      INSERT INTO gold.daily_snapshot (product_id, source_id, date, price_buy_close, price_sell_close)
      SELECT product_id, source_id, @localDate, price_buy, price_sell
      FROM last_ticks
      ON CONFLICT (product_id, source_id, date)
      DO UPDATE SET
        price_buy_close = EXCLUDED.price_buy_close,
        price_sell_close = EXCLUDED.price_sell_close";

    await conn.ExecuteAsync(sql, new { localDate });
  }
}

