using Dapper;
using FluentAssertions;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Application.Services;
using GoldTracker.Domain.Enums;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Persistence;
using GoldTracker.Infrastructure.Persistence.Repositories;
using GoldTracker.Infrastructure.Scrapers.Doji;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace GoldTracker.IntegrationTests;

public sealed class DojiScraperIntegrationTests : IClassFixture<PgFixture>, IDisposable
{
  private readonly PgFixture _fixture;
  private readonly WireMockServer _wireMock;

  public DojiScraperIntegrationTests(PgFixture fixture)
  {
    _fixture = fixture;
    _wireMock = WireMockServer.Start();
  }

  [Fact]
  public async Task RunOnceAsync_should_insert_price_ticks()
  {
    // Setup WireMock to serve DOJI response
    var htmlResponse = await File.ReadAllTextAsync(
      Path.Combine(AppContext.BaseDirectory, "../../../Fixtures/doji/ring-hanoi.html"));
    
    _wireMock
      .Given(Request.Create().WithPath("/gia-vang").UsingGet())
      .RespondWith(Response.Create()
        .WithStatusCode(HttpStatusCode.OK)
        .WithBody(htmlResponse)
        .WithHeader("Content-Type", "text/html"));

    // Configure services
    var services = new ServiceCollection();
    services.AddSingleton(new DapperConnectionFactory(_fixture.ConnectionString));
    services.AddScoped<ISourceRepository, SourceRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<IPriceTickRepository, PriceTickRepository>();
    services.AddScoped<IDailySnapshotRepository, DailySnapshotRepository>();
    
    // Add normalizer dependencies
    services.AddScoped<IPriceNormalizer, PriceNormalizer>();
    
    // Configure DojiOptions to point to WireMock
    services.Configure<DojiOptions>(opts =>
    {
      opts.BaseUrl = _wireMock.Urls[0];
      opts.PriceEndpoint = "/gia-vang";
    });
    
    services.AddSingleton<DojiParser>();
    services.AddHttpClient("doji", (sp, client) =>
    {
      var options = sp.GetRequiredService<IOptions<DojiOptions>>().Value;
      client.BaseAddress = new Uri(options.BaseUrl);
      client.Timeout = TimeSpan.FromSeconds(10);
    });
    
    services.AddScoped<IDojiScraper, DojiScraper>();
    services.AddSingleton<ILogger<DojiScraper>>(new LoggerFactory().CreateLogger<DojiScraper>());

    var provider = services.BuildServiceProvider();
    var scraper = provider.GetRequiredService<IDojiScraper>();

    // Run scraper
    var inserted = await scraper.RunOnceAsync();

    inserted.Should().BeGreaterThan(0);

    // Verify data in database
    var factory = provider.GetRequiredService<DapperConnectionFactory>();
    await using var conn = factory.CreateConnection();
    await conn.OpenAsync();
    
    var count = await conn.QuerySingleAsync<int>(
      @"SELECT COUNT(*) FROM gold.price_tick pt
        JOIN gold.source s ON s.id = pt.source_id
        WHERE s.name = 'DOJI'");
    
    count.Should().BeGreaterThan(0);

    // Verify latest price view
    var latest = await conn.QueryFirstOrDefaultAsync<dynamic>(
      @"SELECT * FROM gold.v_latest_price_per_product v
        JOIN gold.product p ON p.id = v.product_id
        WHERE p.brand = 'DOJI' AND p.form = 'ring'");
    
    latest.Should().NotBeNull();
    Assert.NotNull(latest);
    var priceSellValue = latest!.price_sell;
    if (priceSellValue is not null)
    {
      var priceSell = (decimal)priceSellValue;
      priceSell.Should().BeGreaterThan(0);
    }
  }

  [Fact]
  public async Task DailySnapshot_should_create_snapshot_row()
  {
    var factory = new DapperConnectionFactory(_fixture.ConnectionString);
    var snapshotRepo = new DailySnapshotRepository(factory);
    
    var sourceRepo = new SourceRepository(factory);
    var productRepo = new ProductRepository(factory);
    var tickRepo = new PriceTickRepository(factory);

    // Create test data
    var source = await sourceRepo.EnsureAsync("DOJI", "https://doji.vn");
    var product = await productRepo.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi");
    
    var now = DateTimeOffset.UtcNow;
    var tick = new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = now,
      EffectiveAt = now,
      RawHash = "test_hash_" + Guid.NewGuid().ToString("N")
    };
    
    await tickRepo.InsertAsync(tick);

    // Create snapshot
    var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).Date);
    await snapshotRepo.UpsertDailyCloseAsync(localDate);

    // Verify snapshot exists
    await using var conn = factory.CreateConnection();
    await conn.OpenAsync();
    
    var snapshot = await conn.QueryFirstOrDefaultAsync<dynamic>(
      @"SELECT * FROM gold.daily_snapshot
        WHERE product_id = @pid AND source_id = @sid AND date = @date",
      new { pid = product.Id, sid = source.Id, date = localDate });
    
    snapshot.Should().NotBeNull();
    Assert.NotNull(snapshot);
    var priceSellCloseValue = snapshot!.price_sell_close;
    if (priceSellCloseValue is not null)
    {
      var priceSellClose = (decimal)priceSellCloseValue;
      priceSellClose.Should().Be(7520000);
    }
  }

  public void Dispose()
  {
    _wireMock?.Stop();
    _wireMock?.Dispose();
  }
}

