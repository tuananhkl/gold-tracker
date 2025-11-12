using GoldTracker.Application.Contracts;
using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Application.Queries;
using GoldTracker.Application.Services;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Alerts;
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
    services.AddOptions<AlertsOptions>().Bind(configuration.GetSection(AlertsOptions.SectionName));
    services.AddOptions<TelegramOptions>().Bind(configuration.GetSection(TelegramOptions.SectionName))
      .PostConfigure(opts =>
      {
        // Override from env vars if present
        opts.BotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? opts.BotToken;
        opts.DefaultChatId = Environment.GetEnvironmentVariable("TELEGRAM_DEFAULT_CHAT_ID") ?? opts.DefaultChatId;
      });
    services.AddOptions<ElasticsearchOptions>().Bind(configuration.GetSection(ElasticsearchOptions.SectionName))
      .PostConfigure(opts =>
      {
        opts.BaseUrl = Environment.GetEnvironmentVariable("ELASTICSEARCH__BASEURL") ?? opts.BaseUrl;
        opts.Username = Environment.GetEnvironmentVariable("ELASTICSEARCH__USERNAME") ?? opts.Username;
        opts.Password = Environment.GetEnvironmentVariable("ELASTICSEARCH__PASSWORD") ?? opts.Password;
      });
    
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
    services.AddScoped<IAlertEventRepository, AlertEventRepository>();

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

  public static IServiceCollection AddPhucThanhScraper(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<GoldTracker.Infrastructure.Scrapers.PhucThanh.PhucThanhOptions>()
      .Bind(configuration.GetSection(GoldTracker.Infrastructure.Scrapers.PhucThanh.PhucThanhOptions.SectionName));

    services.AddHttpClient("phucthanh", (sp, client) =>
    {
      var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoldTracker.Infrastructure.Scrapers.PhucThanh.PhucThanhOptions>>().Value;
      client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
      client.DefaultRequestHeaders.Add("User-Agent", "GoldTracker/1.0");
    });

    services.AddSingleton<ScraperHealthTracker>();
    services.AddSingleton<GoldTracker.Infrastructure.Scrapers.PhucThanh.PhucThanhParser>();
    services.AddScoped<GoldTracker.Infrastructure.Scrapers.PhucThanh.IPhucThanhScraper, GoldTracker.Infrastructure.Scrapers.PhucThanh.PhucThanhScraper>();
    return services;
  }

  public static IServiceCollection AddAlerts(this IServiceCollection services, IConfiguration configuration)
  {
    // HTTP client for Elasticsearch
    services.AddHttpClient("elasticsearch", (sp, client) =>
    {
      var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ElasticsearchOptions>>().Value;
      client.BaseAddress = new Uri(options.BaseUrl);
      client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
      if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
      {
        var auth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
      }
    });

    // Alert services
    services.AddScoped<IPriceWindowReader, PriceWindowReader>();
    services.AddScoped<IScraperHealthReader, ScraperHealthReader>();
    services.AddScoped<IAlertEvaluator, AlertEvaluator>();
    services.AddScoped<ITelegramNotifier, TelegramNotifier>();

    // Background services
    var alertsEnabled = bool.Parse(Environment.GetEnvironmentVariable("ALERTS_ENABLED") ?? 
      configuration.GetValue<string>("Alerts:Enabled") ?? "true");
    var telegramEnabled = bool.Parse(Environment.GetEnvironmentVariable("TELEGRAM_ENABLED") ?? 
      configuration.GetValue<string>("Telegram:Enabled") ?? "true");

    if (alertsEnabled)
    {
      services.AddHostedService<AlertsPollingService>();
      services.AddSingleton<DailyBriefService>();
      services.AddHostedService(sp => sp.GetRequiredService<DailyBriefService>());
    }
    else
    {
      services.AddSingleton<DailyBriefService>();
    }

    if (telegramEnabled)
    {
      services.AddHostedService<TelegramBotHostedService>();
    }

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
