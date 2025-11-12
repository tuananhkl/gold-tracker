using System.Text.RegularExpressions;
using GoldTracker.Domain.Normalization;

namespace GoldTracker.Infrastructure.Scrapers.PhucThanh;

public sealed class PhucThanhParser
{
  // Rows we care about with their canonical forms/karats
  private static readonly (string pattern, string form, string karat)[] Targets =
  {
    ("Nhẫn\\s*tròn\\s*9999", "ring", "24"),
    ("Trang\\s*sức\\s*9999", "jewelry", "24"),
    ("Trang\\s*sức\\s*999\\b", "jewelry", "24"),
    ("Trang\\s*sức\\s*99%", "jewelry", "24"),
  };

  // Extract TDs from a row
  private static readonly Regex TdRegex = new(@"<td[^>]*>(.*?)</td>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
  // Capture numeric cell content: 4-6 digits optionally with dot separators
  private static readonly Regex NumericCellRegex = new(@"(?<!\d)(\d{4,6}|\d{1,3}(?:\.\d{3}){1,2})(?!\d)", RegexOptions.Compiled);

  public IReadOnlyList<RawPriceRecord> Parse(string html)
  {
    var results = new List<RawPriceRecord>();
    if (string.IsNullOrWhiteSpace(html)) return results;

    var normalized = html.Replace("&nbsp;", " ");
    foreach (var (pattern, form, karat) in Targets)
    {
      var rowRegex = new Regex($@"{pattern}.*?</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
      var rowMatch = rowRegex.Match(normalized);
      if (!rowMatch.Success) continue;

      // Extract <td> columns and use explicit indices [label, sell, buy]
      var tds = TdRegex.Matches(rowMatch.Value).Select(m => StripTags(m.Groups[1].Value)).ToList();
      if (tds.Count < 3) continue;
      var sellMatch = NumericCellRegex.Match(tds[1]);
      var buyMatch = NumericCellRegex.Match(tds[2]);
      if (!sellMatch.Success || !buyMatch.Success) continue;
      var sellPerChi = ParseNumber(sellMatch.Value) ?? 0m;
      var buyPerChi = ParseNumber(buyMatch.Value) ?? 0m;

      // Site unit: 1.000 VND / chỉ. Convert to VND per cây (10 chỉ)
      var buyVnd = buyPerChi * 1000m * 10m;
      var sellVnd = sellPerChi * 1000m * 10m;

      results.Add(new RawPriceRecord
      {
        SourceName = "PHUC_THANH",
        Brand = "PhucThanh",
        Form = form,
        Karat = karat,
        Region = "Hanoi",
        PriceBuy = buyVnd,
        PriceSell = sellVnd,
        Currency = "VND",
        CollectedAt = DateTimeOffset.UtcNow,
        EffectiveAt = DateTimeOffset.UtcNow
      });
    }

    return results;
  }

  private static decimal? ParseNumber(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return null;
    var cleaned = text.Replace(".", "").Replace(",", "");
    if (decimal.TryParse(cleaned, out var v)) return v;
    return null;
  }

  private static string StripTags(string html)
  {
    return Regex.Replace(html, "<.*?>", " ").Trim();
  }
}


