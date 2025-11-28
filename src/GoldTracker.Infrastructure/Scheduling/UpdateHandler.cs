using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Application.Contracts.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GoldTracker.Infrastructure.Scheduling;

public sealed class UpdateHandler : IUpdateHandler
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<UpdateHandler> _logger;

  public UpdateHandler(IServiceProvider serviceProvider, ILogger<UpdateHandler> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
  {
    if (update.Message is not { } message)
      return;

    var chatId = message.Chat.Id.ToString();
    var userId = message.From?.Id ?? 0;
    var command = message.Text?.Split(' ')[0] ?? "";

    _logger.LogInformation("Received command: {Command} from user {UserId}", command, userId);

    try
    {
      await using var scope = _serviceProvider.CreateAsyncScope();
      var priceRepo = scope.ServiceProvider.GetRequiredService<IPriceTickRepository>();
      var priceReader = scope.ServiceProvider.GetRequiredService<IPriceWindowReader>();

      switch (command)
      {
        case "/start":
          await HandleStartCommand(botClient, message, cancellationToken);
          break;
        case "/ring":
          await HandleRingCommand(botClient, message, priceRepo, cancellationToken);
          break;
        case "/bar":
          await HandleBarCommand(botClient, message, priceRepo, cancellationToken);
          break;
        case "/chart":
          await HandleChartCommand(botClient, message, priceReader, priceRepo, cancellationToken);
          break;
        default:
          await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Lệnh không hợp lệ. Dùng /start để xem hướng dẫn.",
            cancellationToken: cancellationToken);
          break;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling Telegram command: {Command}", command);
      await botClient.SendMessage(
        chatId: message.Chat.Id,
        text: "❌ Có lỗi xảy ra. Vui lòng thử lại sau.",
        cancellationToken: cancellationToken);
    }
  }

  public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
  {
    _logger.LogError(exception, "Telegram polling error from {Source}", errorSource);
    await Task.CompletedTask;
  }

  private static Task HandleStartCommand(ITelegramBotClient botClient, Message message, CancellationToken ct)
  {
    var helpText = @"*Gold Tracker Bot*

Các lệnh có sẵn:
/ring \\- Xem giá nhẫn vàng
/bar \\- Xem giá vàng miếng
/chart \\- Xem biểu đồ giá 24h

Ví dụ:
/chart brand=DOJI region=Hanoi days=7";

    var escaped = helpText
      .Replace("_", "\\_")
      .Replace("*", "\\*")
      .Replace("[", "\\[")
      .Replace("]", "\\]")
      .Replace("(", "\\(")
      .Replace(")", "\\)")
      .Replace("~", "\\~")
      .Replace("`", "\\`")
      .Replace(">", "\\>")
      .Replace("#", "\\#")
      .Replace("+", "\\+")
      .Replace("-", "\\-")
      .Replace("=", "\\=")
      .Replace("|", "\\|")
      .Replace("{", "\\{")
      .Replace("}", "\\}")
      .Replace(".", "\\.")
      .Replace("!", "\\!");

    return botClient.SendMessage(
      chatId: message.Chat.Id,
      text: escaped,
      parseMode: ParseMode.MarkdownV2,
      cancellationToken: ct);
  }

  private async Task HandleRingCommand(ITelegramBotClient botClient, Message message, IPriceTickRepository priceRepo, CancellationToken ct)
  {
    var brands = new[] { "SJC", "DOJI", "BTMC", "PhucThanh" };
    var text = "*Giá Nhẫn Vàng Hôm Nay*\n\n";

    foreach (var brand in brands)
    {
      var latest = await priceRepo.GetLatestAsync("ring", brand, null, ct);
      if (latest.Count > 0)
      {
        var tick = latest[0];
        var dayOverDay = await priceRepo.GetDayOverDayAsync("ring", brand, null, ct);
        var change = dayOverDay.FirstOrDefault();
        
        var changeText = change != default 
          ? $" ({change.DeltaVsYesterday:+N0;-#,0} VND, {change.Direction})"
          : "";
        
        text += $"*{brand}*\n";
        text += $"Mua: {tick.PriceBuy:N0} VND\n";
        text += $"Bán: {tick.PriceSell:N0} VND{changeText}\n\n";
      }
    }

    var escaped = EscapeMarkdown(text);
    await botClient.SendMessage(
      chatId: message.Chat.Id,
      text: escaped,
      parseMode: ParseMode.MarkdownV2,
      cancellationToken: ct);
  }

  private async Task HandleBarCommand(ITelegramBotClient botClient, Message message, IPriceTickRepository priceRepo, CancellationToken ct)
  {
    var brands = new[] { "SJC", "DOJI", "BTMC", "PhucThanh" };
    var text = "*Giá Vàng Miếng Hôm Nay*\n\n";

    foreach (var brand in brands)
    {
      var latest = await priceRepo.GetLatestAsync("bar", brand, null, ct);
      if (latest.Count > 0)
      {
        var tick = latest[0];
        var dayOverDay = await priceRepo.GetDayOverDayAsync("bar", brand, null, ct);
        var change = dayOverDay.FirstOrDefault();
        
        var changeText = change != default 
          ? $" ({change.DeltaVsYesterday:+N0;-#,0} VND, {change.Direction})"
          : "";
        
        text += $"*{brand}*\n";
        text += $"Mua: {tick.PriceBuy:N0} VND\n";
        text += $"Bán: {tick.PriceSell:N0} VND{changeText}\n\n";
      }
    }

    var escaped = EscapeMarkdown(text);
    await botClient.SendMessage(
      chatId: message.Chat.Id,
      text: escaped,
      parseMode: ParseMode.MarkdownV2,
      cancellationToken: ct);
  }

  private async Task HandleChartCommand(ITelegramBotClient botClient, Message message, IPriceWindowReader priceReader, IPriceTickRepository priceRepo, CancellationToken ct)
  {
    // Parse parameters: brand=DOJI region=Hanoi days=7
    var args = message.Text?.Split(' ').Skip(1).ToArray() ?? Array.Empty<string>();
    var brand = "DOJI";
    var region = "Hanoi";
    var days = 7;

    foreach (var arg in args)
    {
      var parts = arg.Split('=');
      if (parts.Length == 2)
      {
        var key = parts[0].ToLowerInvariant();
        var value = parts[1];
        switch (key)
        {
          case "brand":
            brand = value;
            break;
          case "region":
            region = value;
            break;
          case "days":
            if (int.TryParse(value, out var d))
              days = d;
            break;
        }
      }
    }

    // Get history data
    var history = await priceRepo.GetHistoryAsync("ring", days, brand, region, ct);
    if (history.Count == 0)
    {
      await botClient.SendMessage(
        chatId: message.Chat.Id,
        text: $"❌ Không tìm thấy dữ liệu cho {brand} {region}",
        cancellationToken: ct);
      return;
    }

    // Generate simple ASCII chart
    var chart = GenerateAsciiChart(history, brand, region, days);
    var escaped = EscapeMarkdown(chart);

    await botClient.SendMessage(
      chatId: message.Chat.Id,
      text: escaped,
      parseMode: ParseMode.MarkdownV2,
      cancellationToken: ct);
  }

  private static string GenerateAsciiChart(IReadOnlyList<(DateOnly Date, decimal PriceSell)> history, string brand, string region, int days)
  {
    if (history.Count == 0)
      return "Không có dữ liệu";

    var min = history.Min(h => h.PriceSell);
    var max = history.Max(h => h.PriceSell);
    var range = max - min;
    if (range == 0) range = 1;

    var chart = $"*Biểu đồ giá {brand} {region} ({days} ngày)*\n\n";
    chart += "```\n";
    
    const int width = 30;
    foreach (var (date, price) in history.TakeLast(10)) // Show last 10 points
    {
      var barLength = (int)((price - min) / range * width);
      var bar = new string('█', Math.Max(1, barLength));
      chart += $"{date:dd/MM} {price:N0} {bar}\n";
    }
    
    chart += "```\n";
    chart += $"Min: {min:N0} VND | Max: {max:N0} VND";

    return chart;
  }

  private static string EscapeMarkdown(string text)
  {
    return text
      .Replace("_", "\\_")
      .Replace("*", "\\*")
      .Replace("[", "\\[")
      .Replace("]", "\\]")
      .Replace("(", "\\(")
      .Replace(")", "\\)")
      .Replace("~", "\\~")
      .Replace("`", "\\`")
      .Replace(">", "\\>")
      .Replace("#", "\\#")
      .Replace("+", "\\+")
      .Replace("-", "\\-")
      .Replace("=", "\\=")
      .Replace("|", "\\|")
      .Replace("{", "\\{")
      .Replace("}", "\\}")
      .Replace(".", "\\.")
      .Replace("!", "\\!");
  }
}

