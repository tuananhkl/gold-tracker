using GoldTracker.Application.Contracts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Application.Queries;
using GoldTracker.Application.Services;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Config;
using GoldTracker.Infrastructure.Persistence;
using GoldTracker.Infrastructure.Persistence.Repositories;
using GoldTracker.Infrastructure.Scrapers.Doji;
using GoldTracker.Infrastructure.Scheduling;
using GoldTracker.Infrastructure.Scrapers;
using GoldTracker.Infrastructure.Scrapers.Btmc;
using GoldTracker.Infrastructure.Scrapers.Sjc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;

namespace GoldTracker.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGoldTrackerCore(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<SourceOptions>().Bind(configuration.GetSection(SourceOptions.SectionName));
    services.AddOptions<ScheduleOptions>().Bind(configuration.GetSection(ScheduleOptions.SectionName));
    services.AddOptions<DojiOptions>().Bind(configuration.GetSection(DojiOptions.SectionName));
    services.AddOptions<BtmcOptions>().Bind(configuration.GetSection(BtmcOptions.SectionName));
    services.AddOptions<SjcOptions>().Bind(configuration.GetSection(SjcOptions.SectionName));
    
    // Database connection - use IOptions pattern
    services.AddOptions<DbOptions>().Bind(configuration.GetSection(DbOptions.SectionName));
    
    // Get connection string with priority: env var > config > default
    var connString = Environment.GetEnvironmentVariable("POSTGRES_CONN")
      ?? configuration.GetConnectionString("Postgres")
      ?? configuration.GetSection($"{DbOptions.SectionName}:Postgres").Value
      ?? "Host=localhost;Port=5432;Username=gold;Password=gold;Database=gold";
    
    services.AddSingleton(new DapperConnectionFactory(connString));

    // Repositories
    services.AddScoped<ISourceRepository, SourceRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<IPriceTickRepository, PriceTickRepository>();
    services.AddScoped<IDailySnapshotRepository, DailySnapshotRepository>();

    // Normalizer
    services.AddScoped<IPriceNormalizer, PriceNormalizer>();

    // Query services
    services.AddScoped<IPriceQuery, PriceReadService>();
    services.AddScoped<IChangeQuery, PriceReadService>();
    services.AddScoped<ISourceQuery, InMemorySourceService>(); // Keep for now
    
    // V1 API services
    services.AddScoped<GoldTracker.Application.Contracts.IPriceV1Query, GoldTracker.Application.Queries.PriceV1ReadService>();
    services.AddScoped<GoldTracker.Application.Contracts.Repositories.IDbConnectionFactory>(sp => 
      sp.GetRequiredService<DapperConnectionFactory>());

    return services;
  }

  public static IServiceCollection AddDojiScraper(this IServiceCollection services, IConfiguration configuration)
  {
    // HTTP client for DOJI - retry logic handled in DojiScraper
    services.AddHttpClient("doji", (sp, client) =>
    {
      var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DojiOptions>>().Value;
      client.BaseAddress = new Uri(options.BaseUrl);
      client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
      client.DefaultRequestHeaders.Add("User-Agent", "GoldTracker/1.0");
    });

    services.AddSingleton<DojiParser>();
    services.AddScoped<IDojiScraper, DojiScraper>();

    return services;
  }

  public static IServiceCollection AddBtmcScraper(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddHttpClient("btmc", (sp, client) =>
    {
      var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BtmcOptions>>().Value;
      client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
      client.DefaultRequestHeaders.Add("User-Agent", "GoldTracker/1.0");
    });

    services.AddSingleton<ScraperHealthTracker>();
    services.AddSingleton<BtmcParser>();
    services.AddScoped<IBtmcScraper, BtmcScraper>();

    return services;
  }

  public static IServiceCollection AddSjcScraper(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddHttpClient("sjc", (sp, client) =>
    {
      var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SjcOptions>>().Value;
      client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
      client.DefaultRequestHeaders.Add("User-Agent", "GoldTracker/1.0");
    });

    services.AddSingleton<ScraperHealthTracker>();
    services.AddSingleton<SjcParser>();
    services.AddScoped<ISjcScraper, SjcScraper>();
    return services;
  }

  public static IServiceCollection AddScheduling(this IServiceCollection services)
  {
    var scraperEnabled = bool.Parse(Environment.GetEnvironmentVariable("SCRAPER_ENABLED") ?? "false");
    var snapshotEnabled = bool.Parse(Environment.GetEnvironmentVariable("SNAPSHOT_ENABLED") ?? "false");

    if (scraperEnabled)
    {
      services.AddHostedService<TenMinuteScrapeService>();
    }

    if (snapshotEnabled)
    {
      services.AddHostedService<DailySnapshotService>();
    }

    return services;
  }
}
