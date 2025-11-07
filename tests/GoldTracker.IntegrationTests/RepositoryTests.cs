using Dapper;
using FluentAssertions;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Application.Services;
using GoldTracker.Domain.Enums;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Persistence;
using GoldTracker.Infrastructure.Persistence.Repositories;
using Xunit;

namespace GoldTracker.IntegrationTests;

public class RepositoryTests : IClassFixture<PgFixture>
{
  private readonly PgFixture _fixture;
  private readonly DapperConnectionFactory _factory;
  private readonly ISourceRepository _sourceRepo;
  private readonly IProductRepository _productRepo;
  private readonly IPriceTickRepository _tickRepo;
  private readonly IDailySnapshotRepository _snapshotRepo;
  private readonly IPriceNormalizer _normalizer;

  public RepositoryTests(PgFixture fixture)
  {
    _fixture = fixture;
    _factory = new DapperConnectionFactory(fixture.ConnectionString);
    _sourceRepo = new SourceRepository(_factory);
    _productRepo = new ProductRepository(_factory);
    _tickRepo = new PriceTickRepository(_factory);
    _snapshotRepo = new DailySnapshotRepository(_factory);
    _normalizer = new PriceNormalizer(_sourceRepo, _productRepo);
  }

  [Fact]
  public async Task Insert_and_dedup_should_respect_unique_constraint()
  {
    var source = await _sourceRepo.EnsureAsync("DOJI", "https://doji.vn", CancellationToken.None);
    var product = await _productRepo.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi", CancellationToken.None);

    var now = DateTimeOffset.UtcNow;
    var tick1 = new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = now,
      EffectiveAt = now,
      RawHash = "testhash1"
    };

    await _tickRepo.InsertAsync(tick1, CancellationToken.None);

    // Insert same tick again - should be ignored
    await _tickRepo.InsertAsync(tick1, CancellationToken.None);

    // Verify only one row exists
    var count = await _factory.CreateConnection().QuerySingleAsync<int>(
      "SELECT COUNT(*) FROM gold.price_tick WHERE product_id = @pid AND source_id = @sid AND effective_at = @eff",
      new { pid = product.Id, sid = source.Id, eff = now });
    count.Should().Be(1);
  }

  [Fact]
  public async Task GetLatest_should_return_latest_price()
  {
    var source = await _sourceRepo.EnsureAsync("DOJI", "https://doji.vn", CancellationToken.None);
    var product = await _productRepo.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi", CancellationToken.None);

    var day1 = new DateTimeOffset(2025, 11, 1, 9, 0, 0, TimeSpan.Zero);
    var day2 = new DateTimeOffset(2025, 11, 2, 16, 30, 0, TimeSpan.Zero);

    await _tickRepo.InsertAsync(new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7350000,
      PriceSell = 7450000,
      Currency = "VND",
      CollectedAt = day1,
      EffectiveAt = day1,
      RawHash = "hash1"
    }, CancellationToken.None);

    await _tickRepo.InsertAsync(new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = day2,
      EffectiveAt = day2,
      RawHash = "hash2"
    }, CancellationToken.None);

    var latest = await _tickRepo.GetLatestAsync("ring", "DOJI", "Hanoi", CancellationToken.None);
    latest.Should().HaveCount(1);
    latest[0].PriceSell.Should().Be(7520000);
  }

  [Fact]
  public async Task GetHistory_should_return_ordered_series()
  {
    var source = await _sourceRepo.EnsureAsync("DOJI", "https://doji.vn", CancellationToken.None);
    var product = await _productRepo.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi", CancellationToken.None);

    var day1 = new DateTimeOffset(2025, 11, 1, 16, 30, 0, TimeSpan.Zero);
    var day2 = new DateTimeOffset(2025, 11, 2, 16, 30, 0, TimeSpan.Zero);

    await _tickRepo.InsertAsync(new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7380000,
      PriceSell = 7480000,
      Currency = "VND",
      CollectedAt = day1,
      EffectiveAt = day1,
      RawHash = "hash1"
    }, CancellationToken.None);

    await _tickRepo.InsertAsync(new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = day2,
      EffectiveAt = day2,
      RawHash = "hash2"
    }, CancellationToken.None);

    var history = await _tickRepo.GetHistoryAsync("ring", 30, "DOJI", "Hanoi", CancellationToken.None);
    history.Should().HaveCountGreaterThanOrEqualTo(2);
    history[0].Date.Should().BeBefore(history[1].Date);
  }

  [Fact]
  public async Task GetDayOverDay_should_compute_delta_and_direction()
  {
    var source = await _sourceRepo.EnsureAsync("DOJI", "https://doji.vn", CancellationToken.None);
    var product = await _productRepo.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi", CancellationToken.None);

    var day1 = new DateTimeOffset(2025, 11, 1, 16, 30, 0, TimeSpan.Zero);
    var day2 = new DateTimeOffset(2025, 11, 2, 16, 30, 0, TimeSpan.Zero);

    await _tickRepo.InsertAsync(new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7380000,
      PriceSell = 7480000,
      Currency = "VND",
      CollectedAt = day1,
      EffectiveAt = day1,
      RawHash = "hash1"
    }, CancellationToken.None);

    await _tickRepo.InsertAsync(new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = day2,
      EffectiveAt = day2,
      RawHash = "hash2"
    }, CancellationToken.None);

    // Create snapshots
    await _snapshotRepo.UpsertDailyCloseAsync(DateOnly.FromDateTime(day1.Date), CancellationToken.None);
    await _snapshotRepo.UpsertDailyCloseAsync(DateOnly.FromDateTime(day2.Date), CancellationToken.None);

    var changes = await _tickRepo.GetDayOverDayAsync("ring", "DOJI", "Hanoi", CancellationToken.None);
    changes.Should().NotBeEmpty();
    var day2Change = changes.FirstOrDefault(c => c.Date == DateOnly.FromDateTime(day2.Date));
    day2Change.Should().NotBeNull();
    day2Change!.DeltaVsYesterday.Should().Be(40000);
    day2Change.Direction.Should().Be("up");
  }

  [Fact]
  public async Task UpsertDailyClose_should_create_snapshot()
  {
    var source = await _sourceRepo.EnsureAsync("DOJI", "https://doji.vn", CancellationToken.None);
    var product = await _productRepo.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi", CancellationToken.None);

    var day2 = new DateTimeOffset(2025, 11, 2, 16, 30, 0, TimeSpan.Zero);
    await _tickRepo.InsertAsync(new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = day2,
      EffectiveAt = day2,
      RawHash = "hash1"
    }, CancellationToken.None);

    await _snapshotRepo.UpsertDailyCloseAsync(DateOnly.FromDateTime(day2.Date), CancellationToken.None);

    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync();
    var snapshot = await conn.QueryFirstOrDefaultAsync<dynamic>(
      "SELECT * FROM gold.daily_snapshot WHERE product_id = @pid AND source_id = @sid AND date = @date",
      new { pid = product.Id, sid = source.Id, date = DateOnly.FromDateTime(day2.Date) });

    if (snapshot is null) throw new InvalidOperationException("Snapshot not found");
    var priceSellClose = (decimal)snapshot.price_sell_close;
    priceSellClose.Should().Be(7520000);
  }
}

