namespace GoldTracker.Domain.Abstractions;

public interface IClock
{
  DateTimeOffset UtcNow { get; }
}
