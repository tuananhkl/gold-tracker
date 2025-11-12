namespace GoldTracker.Infrastructure.Config;

public sealed class TelegramOptions
{
  public const string SectionName = "Telegram";

  public bool Enabled { get; set; } = true;
  public string BotToken { get; set; } = string.Empty;
  public string DefaultChatId { get; set; } = string.Empty;
}

