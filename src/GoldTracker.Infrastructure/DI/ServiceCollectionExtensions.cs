using GoldTracker.Application.Contracts;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Application.Queries;
using GoldTracker.Application.Services;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Config;
using GoldTracker.Infrastructure.Persistence;
using GoldTracker.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoldTracker.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGoldTrackerCore(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<SourceOptions>().Bind(configuration.GetSection(SourceOptions.SectionName));
    services.AddOptions<ScheduleOptions>().Bind(configuration.GetSection(ScheduleOptions.SectionName));

    // Database connection
    var connString = configuration.GetConnectionString("Postgres")
      ?? Environment.GetEnvironmentVariable("POSTGRES_CONN")
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

    return services;
  }
}
