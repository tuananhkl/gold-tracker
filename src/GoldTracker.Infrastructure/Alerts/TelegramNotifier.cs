using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GoldTracker.Infrastructure.Alerts;

public sealed class TelegramNotifier : ITelegramNotifier
{
  private readonly TelegramBotClient? _botClient;
  private readonly TelegramOptions _options;
  private readonly ILogger<TelegramNotifier> _logger;

  public TelegramNotifier(
    IOptions<TelegramOptions> options,
    ILogger<TelegramNotifier> logger)
  {
    _options = options.Value;
    _logger = logger;

    if (IsConfigured(_options))
    {
      _botClient = new TelegramBotClient(_options.BotToken);
    }
    else
    {
      _logger.LogDebug("Telegram notifier disabled: missing token or chat id");
      _botClient = null;
    }
  }

  public async Task SendTextAsync(string chatId, string text, CancellationToken ct = default)
  {
    if (!IsConfigured(_options) || _botClient is null || string.IsNullOrWhiteSpace(chatId) || chatId == "__FROM_ENV__")
    {
      _logger.LogDebug("Skipping Telegram message; notifier not configured");
      return;
    }

    try
    {
      await _botClient.SendMessage(
        chatId: chatId,
        text: text,
        parseMode: ParseMode.MarkdownV2,
        cancellationToken: ct);
      
      _logger.LogInformation("Sent Telegram message to {ChatId}", chatId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send Telegram message to {ChatId}", chatId);
    }
  }

  public async Task SendPhotoAsync(string chatId, byte[] photoBytes, string? caption, CancellationToken ct = default)
  {
    if (!IsConfigured(_options) || _botClient is null || string.IsNullOrWhiteSpace(chatId) || chatId == "__FROM_ENV__")
    {
      _logger.LogDebug("Skipping Telegram photo; notifier not configured");
      return;
    }

    try
    {
      using var stream = new MemoryStream(photoBytes);
      var inputFile = InputFile.FromStream(stream, "chart.png");
      await _botClient.SendPhoto(
        chatId: chatId,
        photo: inputFile,
        caption: caption,
        parseMode: ParseMode.MarkdownV2,
        cancellationToken: ct);
      
      _logger.LogInformation("Sent Telegram photo to {ChatId}", chatId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send Telegram photo to {ChatId}", chatId);
    }
  }

  private static bool IsConfigured(TelegramOptions options) =>
    options.Enabled &&
    !string.IsNullOrWhiteSpace(options.BotToken) &&
    options.BotToken != "__FROM_ENV__" &&
    !string.IsNullOrWhiteSpace(options.DefaultChatId) &&
    options.DefaultChatId != "__FROM_ENV__";
}

