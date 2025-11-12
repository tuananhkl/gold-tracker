using GoldTracker.Infrastructure.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoldTracker.Infrastructure.Scheduling;

public sealed class TelegramBotHostedService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly TelegramOptions _options;
  private readonly ILogger<TelegramBotHostedService> _logger;
  private TelegramBotClient? _botClient;

  public TelegramBotHostedService(
    IServiceProvider serviceProvider,
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotHostedService> logger)
  {
    _serviceProvider = serviceProvider;
    _options = options.Value;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!IsConfigured(_options))
    {
      _logger.LogInformation("Telegram bot is disabled or token not configured");
      return;
    }

    _botClient = new TelegramBotClient(_options.BotToken);
    var receiverOptions = new ReceiverOptions
    {
      AllowedUpdates = Array.Empty<UpdateType>()
    };

    var me = await _botClient.GetMe(stoppingToken);
    _logger.LogInformation("TelegramBotHostedService started. Bot username: @{Username}", me.Username);

    var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
    var updateHandlerLogger = loggerFactory.CreateLogger<UpdateHandler>();
    var updateHandler = new UpdateHandler(_serviceProvider, updateHandlerLogger);
    _botClient.StartReceiving(
      updateHandler: updateHandler,
      receiverOptions: receiverOptions,
      cancellationToken: stoppingToken);
    
    // Keep service running
    await Task.Delay(Timeout.Infinite, stoppingToken);
  }
  private static bool IsConfigured(TelegramOptions options) =>
    options.Enabled &&
    !string.IsNullOrWhiteSpace(options.BotToken) && options.BotToken != "__FROM_ENV__";

}
