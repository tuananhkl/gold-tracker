namespace GoldTracker.Application.Queries;

public record LatestQuery(string? Kind, string? Brand, string? Region);

public record HistoryQuery(
  string? Kind,
  string? Brand,
  string? Region,
  DateOnly? From = null,
  DateOnly? To = null,
  int? Days = 30  // default 30 days
);

public record ChangesQuery(string? Kind, string? Brand, string? Region);

public record ByDateQuery(
  DateOnly Date,
  string? Kind = null,
  string? Brand = null,
  string? Region = null
);

