using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Enums;
using GoldTracker.Domain.Normalization;

namespace GoldTracker.Application.Services;

public sealed class PriceNormalizer : IPriceNormalizer
{
  private readonly ISourceRepository _sourceRepo;
  private readonly IProductRepository _productRepo;

  public PriceNormalizer(ISourceRepository sourceRepo, IProductRepository productRepo)
  {
    _sourceRepo = sourceRepo;
    _productRepo = productRepo;
  }

  public async Task<(Guid ProductId, Guid SourceId, CanonicalPriceTick Tick)> NormalizeAsync(
    RawPriceRecord raw,
    CancellationToken ct = default)
  {
    // Normalize source
    var sourceName = NormalizeString(raw.SourceName);
    if (string.IsNullOrWhiteSpace(sourceName))
      throw new ArgumentException("Source name is required", nameof(raw));

    var source = await _sourceRepo.EnsureAsync(sourceName, $"https://{sourceName.ToLowerInvariant()}.vn", ct)
      ?? throw new InvalidOperationException($"Failed to ensure source: {sourceName}");

    // Normalize brand
    var brand = NormalizeString(raw.Brand) ?? throw new ArgumentException("Brand is required", nameof(raw));

    // Normalize form
    var form = NormalizeForm(raw.Form ?? string.Empty);

    // Normalize karat
    var karat = NormalizeKarat(raw.Karat);

    // Normalize region
    var region = NormalizeString(raw.Region);

    // Normalize currency
    var currency = NormalizeCurrency(raw.Currency ?? "VND");

    // Validate prices
    var priceBuy = raw.PriceBuy ?? throw new ArgumentException("PriceBuy is required", nameof(raw));
    var priceSell = raw.PriceSell ?? throw new ArgumentException("PriceSell is required", nameof(raw));

    if (priceBuy <= 0)
      throw new ArgumentException("PriceBuy must be > 0", nameof(raw));
    if (priceSell <= 0)
      throw new ArgumentException("PriceSell must be > 0", nameof(raw));
    if (priceSell < priceBuy)
      throw new ArgumentException("PriceSell must be >= PriceBuy", nameof(raw));

    // Validate timestamps
    var collectedAt = raw.CollectedAt ?? throw new ArgumentException("CollectedAt is required", nameof(raw));
    var effectiveAt = raw.EffectiveAt ?? throw new ArgumentException("EffectiveAt is required", nameof(raw));

    // Find or create product
    var product = await _productRepo.FindOrCreateAsync(brand, form, karat, region, ct)
      ?? throw new InvalidOperationException($"Failed to create product: {brand}, {form}, {karat}, {region}");

    // Compute raw hash
    var hashPayload = new Dictionary<string, object?>
    {
      ["brand"] = brand,
      ["form"] = form.ToString(),
      ["karat"] = karat,
      ["region"] = region,
      ["priceBuy"] = priceBuy,
      ["priceSell"] = priceSell,
      ["currency"] = currency,
      ["effectiveAt"] = effectiveAt.ToString("O")
    };

    var json = JsonSerializer.Serialize(hashPayload, new JsonSerializerOptions { WriteIndented = false });
    var hash = ComputeSha256(json);

    var tick = new CanonicalPriceTick
    {
      ProductId = product.Id,
      SourceId = source.Id,
      PriceBuy = priceBuy,
      PriceSell = priceSell,
      Currency = currency,
      CollectedAt = collectedAt,
      EffectiveAt = effectiveAt,
      RawHash = hash
    };

    return (product.Id, source.Id, tick);
  }

  private static string NormalizeString(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

  private static GoldForm NormalizeForm(string form)
  {
    var normalized = form.Trim().ToLowerInvariant();
    if (normalized.Contains("ring") || normalized.Contains("nhẫn"))
      return GoldForm.Ring;
    if (normalized.Contains("bar") || normalized.Contains("miếng") || normalized.Contains("vàng miếng"))
      return GoldForm.Bar;
    if (normalized.Contains("jewelry") || normalized.Contains("trang sức"))
      return GoldForm.Jewelry;
    return GoldForm.Other;
  }

  private static int? NormalizeKarat(string? karat)
  {
    if (string.IsNullOrWhiteSpace(karat))
      return null;

    var normalized = karat.Trim().ToUpperInvariant();
    // Remove "K" suffix if present
    normalized = normalized.Replace("K", string.Empty, StringComparison.OrdinalIgnoreCase);

    // Handle "9999" -> 24 (pure gold)
    if (normalized == "9999" || normalized == "999")
      return 24;

    if (int.TryParse(normalized, out var value) && value > 0)
      return value;

    return null;
  }

  private static string NormalizeCurrency(string currency)
  {
    var normalized = currency.Trim().ToUpperInvariant();
    if (normalized.Length != 3)
      throw new ArgumentException($"Currency must be 3-letter code, got: {currency}", nameof(currency));
    return normalized;
  }

  private static string ComputeSha256(string input)
  {
    var bytes = Encoding.UTF8.GetBytes(input);
    var hash = SHA256.HashData(bytes);
    return Convert.ToHexString(hash).ToLowerInvariant();
  }
}

