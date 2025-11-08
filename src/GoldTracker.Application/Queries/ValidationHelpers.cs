namespace GoldTracker.Application.Queries;

public static class ValidationHelpers
{
  private static readonly HashSet<string> ValidKinds = new(StringComparer.OrdinalIgnoreCase)
  {
    "ring", "bar", "jewelry", "other"
  };

  public static (bool IsValid, string? ErrorMessage) ValidateLatestQuery(string? kind, string? brand, string? region)
  {
    if (!string.IsNullOrWhiteSpace(kind) && !ValidKinds.Contains(kind))
    {
      return (false, $"Invalid 'kind' parameter. Must be one of: ring, bar, jewelry, other");
    }

    if (!string.IsNullOrWhiteSpace(brand) && brand.Trim().Length > 64)
    {
      return (false, "Brand parameter must be 64 characters or less");
    }

    if (!string.IsNullOrWhiteSpace(region) && region.Trim().Length > 64)
    {
      return (false, "Region parameter must be 64 characters or less");
    }

    return (true, null);
  }

  public static (bool IsValid, string? ErrorMessage) ValidateHistoryQuery(string? kind, string? brand, string? region, DateOnly? from, DateOnly? to, int? days)
  {
    var (isValid, error) = ValidateLatestQuery(kind, brand, region);
    if (!isValid)
      return (false, error);

    if (days.HasValue && (days.Value < 1 || days.Value > 365))
    {
      return (false, "Days parameter must be between 1 and 365");
    }

    if (from.HasValue && to.HasValue && from.Value > to.Value)
    {
      return (false, "From date must be before or equal to To date");
    }

    return (true, null);
  }

  public static (bool IsValid, string? ErrorMessage) ValidateChangesQuery(string? kind, string? brand, string? region)
  {
    return ValidateLatestQuery(kind, brand, region);
  }

  public static (bool IsValid, string? ErrorMessage) ValidateByDateQuery(DateOnly date, string? kind, string? brand, string? region)
  {
    var (isValid, error) = ValidateLatestQuery(kind, brand, region);
    if (!isValid)
      return (false, error);

    // Validate date is not too far in the future
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (date > today.AddDays(1))
    {
      return (false, "Date cannot be more than 1 day in the future");
    }

    // Validate date is not too far in the past (optional, but reasonable limit)
    var minDate = today.AddYears(-10);
    if (date < minDate)
    {
      return (false, "Date cannot be more than 10 years in the past");
    }

    return (true, null);
  }

  public static string NormalizeKind(string? kind)
  {
    if (string.IsNullOrWhiteSpace(kind))
      return "ring"; // Default to ring

    return ValidKinds.Contains(kind) ? kind.ToLowerInvariant() : kind.Trim();
  }
}

