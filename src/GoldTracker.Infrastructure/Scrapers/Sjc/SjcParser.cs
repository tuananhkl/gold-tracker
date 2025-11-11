using System.Globalization;
using System.Text.Json;
using GoldTracker.Domain.Normalization;

namespace GoldTracker.Infrastructure.Scrapers.Sjc;

public sealed class SjcParser
{
  private static readonly TimeZoneInfo VietnamTimeZone =
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

  public IReadOnlyList<RawPriceRecord> Parse(string json)
  {
    var records = new List<RawPriceRecord>();
    if (string.IsNullOrWhiteSpace(json))
      return records;

    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
      return records;

    var collectedAt = DateTimeOffset.UtcNow;
    // Try to parse 'latestDate' like "08:30 11/11/2025"
    DateTimeOffset? latest = null;
    if (root.TryGetProperty("latestDate", out var latestProp) && latestProp.ValueKind == JsonValueKind.String)
    {
      var text = latestProp.GetString();
      if (!string.IsNullOrWhiteSpace(text) &&
          DateTime.TryParseExact(text!.Trim(), "HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var localDt))
      {
        var offset = VietnamTimeZone.GetUtcOffset(localDt);
        latest = new DateTimeOffset(localDt, offset).ToUniversalTime();
      }
    }

    foreach (var item in data.EnumerateArray())
    {
      var typeName = item.TryGetProperty("TypeName", out var tn) ? tn.GetString() : null;
      var branch = item.TryGetProperty("BranchName", out var br) ? br.GetString() : null;
      var buy = item.TryGetProperty("BuyValue", out var bv) ? bv.GetDecimal() : 0m;
      var sell = item.TryGetProperty("SellValue", out var sv) ? sv.GetDecimal() : 0m;

      if (buy <= 0 || sell <= 0) continue;

      var effectiveAt = latest ?? collectedAt;

      records.Add(new RawPriceRecord
      {
        SourceName = "SJC",
        Brand = "SJC",
        Form = NormalizeForm(typeName ?? string.Empty),
        Karat = NormalizeKarat(typeName ?? string.Empty),
        Region = NormalizeRegion(branch ?? string.Empty),
        PriceBuy = buy,
        PriceSell = sell,
        Currency = "VND",
        CollectedAt = collectedAt,
        EffectiveAt = effectiveAt
      });
    }

    return records;
  }

  private static string NormalizeForm(string text)
  {
    var lower = text.ToLowerInvariant();
    if (lower.Contains("nhẫn") || lower.Contains("nhan")) return "ring";
    if (lower.Contains("nữ trang") || lower.Contains("nu trang") || lower.Contains("jewelry")) return "jewelry";
    return "bar"; // SJC 1L, 10L, 1KG etc.
  }

  private static string NormalizeKarat(string text)
  {
    var lower = text.ToLowerInvariant();
    if (lower.Contains("999,99") || lower.Contains("999.99") || lower.Contains("9999")) return "24";
    // fallbacks
    var m = System.Text.RegularExpressions.Regex.Match(text, @"\b(18|20|22|24)\b");
    return m.Success ? m.Groups[1].Value : "24";
  }

  private static string NormalizeRegion(string text)
  {
    var lower = text.ToLowerInvariant();
    if (lower.Contains("hồ chí minh") || lower.Contains("ho chi minh")) return "HCMC";
    if (lower.Contains("hà nội") || lower.Contains("ha noi")) return "Hanoi";
    // Accept province names as-is but default to Hanoi to avoid null
    return "Hanoi";
  }
}

