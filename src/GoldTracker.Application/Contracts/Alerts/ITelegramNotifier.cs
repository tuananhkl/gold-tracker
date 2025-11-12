namespace GoldTracker.Application.Contracts.Alerts;

public interface ITelegramNotifier
{
  Task SendTextAsync(string chatId, string text, CancellationToken ct = default);
  Task SendPhotoAsync(string chatId, byte[] photoBytes, string? caption, CancellationToken ct = default);
}

