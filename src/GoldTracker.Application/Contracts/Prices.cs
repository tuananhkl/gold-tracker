namespace GoldTracker.Application.Contracts;

public record PriceItemDto(
  Guid ProductId,
  string Brand,
  string Form,          // "ring" | "bar" | "jewelry" | "other"
  int? Karat,           // 24 for ring/bar in VN (nullable)
  string Region,        // "Hanoi" | "HCMC" | ...
  string Source,        // e.g., "DOJI"
  decimal PriceBuy,
  decimal PriceSell,
  string Currency,      // "VND"
  DateTimeOffset EffectiveAt,
  DateTimeOffset CollectedAt
);

public record LatestPricesResponse(IReadOnlyList<PriceItemDto> Items);

public record PriceHistoryPointDto(
  DateOnly Date,        // local day (Asia/Ho_Chi_Minh)
  decimal PriceBuyClose,
  decimal PriceSellClose
);

public record PriceHistoryResponse(
  Guid ProductId,
  string Brand,
  string Form,
  int? Karat,
  string Region,
  string Source,
  IReadOnlyList<PriceHistoryPointDto> Points
);

public record ChangeItemDto(
  Guid ProductId,
  string Brand,
  string Form,
  int? Karat,
  string Region,
  string Source,
  DateOnly Date,
  decimal PriceSellClose,
  decimal DeltaVsYesterday,
  string Direction      // "up" | "down" | "flat"
);

public record PriceChangesResponse(IReadOnlyList<ChangeItemDto> Items);

