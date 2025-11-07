using System.Globalization;

namespace GoldTracker.Infrastructure.Scheduling;

public static class TimeWindow
{
  public static bool IsInWindow(TimeOnly localTime, TimeOnly start, TimeOnly end)
  {
    if (start <= end)
      return localTime >= start && localTime <= end;
    // Handle overnight window (e.g., 22:00 - 06:00)
    return localTime >= start || localTime <= end;
  }

  public static TimeOnly ParseTime(string timeStr, TimeOnly defaultValue)
  {
    if (string.IsNullOrWhiteSpace(timeStr))
      return defaultValue;
    
    if (TimeOnly.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
      return time;
    
    return defaultValue;
  }
}

