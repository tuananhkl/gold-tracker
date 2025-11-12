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
  private readonly TelegramBotClient _botClient;
  private readonly TelegramOptions _options;
  private readonly ILogger<TelegramNotifier> _logger;

  public TelegramNotifier(
    IOptions<TelegramOptions> options,
    ILogger<TelegramNotifier> logger)
  {
    _options = options.Value;
    _logger = logger;
    _botClient = new TelegramBotClient(_options.BotToken);
  }

  public async Task SendTextAsync(string chatId, string text, CancellationToken ct = default)
  {
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
      throw;
    }
  }

  public async Task SendPhotoAsync(string chatId, byte[] photoBytes, string? caption, CancellationToken ct = default)
  {
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
      throw;
    }
  }
}

