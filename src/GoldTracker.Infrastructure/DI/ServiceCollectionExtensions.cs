using GoldTracker.Application.Contracts;
using GoldTracker.Application.Services;
using GoldTracker.Infrastructure.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoldTracker.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGoldTrackerCore(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<SourceOptions>().Bind(configuration.GetSection(SourceOptions.SectionName));
    services.AddOptions<ScheduleOptions>().Bind(configuration.GetSection(ScheduleOptions.SectionName));

    services.AddSingleton<IPriceQuery, InMemoryPriceService>();
    services.AddSingleton<IChangeQuery, InMemoryPriceService>();
    services.AddSingleton<ISourceQuery, InMemorySourceService>();

    return services;
  }
}
