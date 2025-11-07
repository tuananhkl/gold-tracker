using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using GoldTracker.Domain.Normalization;

namespace GoldTracker.Infrastructure.Scrapers.Doji;

public sealed class DojiParser
{
  public IReadOnlyList<RawPriceRecord> ParseHtml(string html, string sourceName = "DOJI")
  {
    var context = BrowsingContext.New(Configuration.Default);
    var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();
    
    var records = new List<RawPriceRecord>();
    var now = DateTimeOffset.UtcNow;

    // Try table rows first (tr > td structure)
    var tableRows = document.QuerySelectorAll("table tr");
    foreach (var row in tableRows)
    {
      var cells = row.QuerySelectorAll("td").ToList();
      if (cells.Count < 4) continue; // Need at least 4 cells: form/karat, region, buy, sell

      var formKaratText = cells[0].TextContent?.Trim() ?? string.Empty;
      var regionText = cells[1].TextContent?.Trim() ?? string.Empty;
      var buyText = cells[2].TextContent?.Trim() ?? string.Empty;
      var sellText = cells[3].TextContent?.Trim() ?? string.Empty;

      var form = NormalizeForm(formKaratText);
      if (form is null) continue;

      var karat = NormalizeKarat(formKaratText);
      var region = NormalizeRegion(regionText);
      var priceBuy = ParsePrice(buyText);
      var priceSell = ParsePrice(sellText);

      if (priceBuy <= 0 || priceSell <= 0) continue;

      records.Add(new RawPriceRecord
      {
        SourceName = sourceName,
        Brand = "DOJI",
        Form = form,
        Karat = karat,
        Region = region,
        PriceBuy = priceBuy,
        PriceSell = priceSell,
        Currency = "VND",
        CollectedAt = now,
        EffectiveAt = now
      });
    }

    // If no table rows found, try div/span structure (.price-row > span)
    if (records.Count == 0)
    {
      var divRows = document.QuerySelectorAll(".price-row, div[class*='price']");
      foreach (var row in divRows)
      {
        var spans = row.QuerySelectorAll("span").ToList();
        if (spans.Count < 4) continue; // Need at least 4 spans

        var formKaratText = spans[0].TextContent?.Trim() ?? string.Empty;
        var regionText = spans[1].TextContent?.Trim() ?? string.Empty;
        var buyText = spans[2].TextContent?.Trim() ?? string.Empty;
        var sellText = spans[3].TextContent?.Trim() ?? string.Empty;

        var form = NormalizeForm(formKaratText);
        if (form is null) continue;

        var karat = NormalizeKarat(formKaratText);
        var region = NormalizeRegion(regionText);
        var priceBuy = ParsePrice(buyText);
        var priceSell = ParsePrice(sellText);

        if (priceBuy <= 0 || priceSell <= 0) continue;

        records.Add(new RawPriceRecord
        {
          SourceName = sourceName,
          Brand = "DOJI",
          Form = form,
          Karat = karat,
          Region = region,
          PriceBuy = priceBuy,
          PriceSell = priceSell,
          Currency = "VND",
          CollectedAt = now,
          EffectiveAt = now
        });
      }
    }

    // Regex fallback: scan raw HTML text for common patterns
    if (records.Count == 0)
    {
      var text = html.Replace("\n", " ").Replace("\r", " ");
      var now2 = DateTimeOffset.UtcNow;

      // Pattern targets: label (nhẫn/vàng miếng) + optional karat + region + two prices
      var patterns = new[]
      {
        // e.g. "Nhẫn tròn trơn 24K ... Hà Nội ... 7,420,000 ... 7,520,000"
        @"(nhẫn[^<]*?)(hà\s*nội|hanoi|hồ\s*chí\s*minh|ho\s*chi\s*minh|hcmc|hcm)[^\d]{0,40}([\d\.,]{5,})[^\d]{0,20}([\d\.,]{5,})",
        // e.g. "Vàng miếng 9999 ... HCMC ... 7.5xx.000 ... 7.6xx.000"
        @"(vàng\s*miếng[^<]*?)(hà\s*nội|hanoi|hồ\s*chí\s*minh|ho\s*chi\s*minh|hcmc|hcm)[^\d]{0,40}([\d\.,]{5,})[^\d]{0,20}([\d\.,]{5,})"
      };

      foreach (var pat in patterns)
      {
        var rx = System.Text.RegularExpressions.Regex.Matches(text, pat, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in rx)
        {
          if (!m.Success || m.Groups.Count < 5) continue;
          var formRaw = m.Groups[1].Value;
          var regionRaw = m.Groups[2].Value;
          var buyRaw = m.Groups[3].Value;
          var sellRaw = m.Groups[4].Value;

          var form = NormalizeForm(formRaw);
          if (form is null) continue;
          var karat = NormalizeKarat(formRaw);
          var region = NormalizeRegion(regionRaw);
          var priceBuy = ParsePrice(buyRaw);
          var priceSell = ParsePrice(sellRaw);
          if (priceBuy <= 0 || priceSell <= 0) continue;

          records.Add(new RawPriceRecord
          {
            SourceName = sourceName,
            Brand = "DOJI",
            Form = form,
            Karat = karat,
            Region = region,
            PriceBuy = priceBuy,
            PriceSell = priceSell,
            Currency = "VND",
            CollectedAt = now2,
            EffectiveAt = now2
          });
        }
      }
    }

    return records;
  }

  public IReadOnlyList<RawPriceRecord> ParseJson(string json, string sourceName = "DOJI")
  {
    var records = new List<RawPriceRecord>();
    var now = DateTimeOffset.UtcNow;

    // Try to parse JSON structure (adjust based on actual DOJI API response)
    using var doc = System.Text.Json.JsonDocument.Parse(json);
    var root = doc.RootElement;

    // Common JSON structures: array of items, or nested data.items
    JsonElement items;
    if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
      items = root;
    else if (root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Array)
      items = data;
    else if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
      items = itemsProp;
    else
      return records;

    foreach (var item in items.EnumerateArray())
    {
      var form = item.TryGetProperty("form", out var formProp) ? formProp.GetString() 
        : item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() 
        : null;
      
      if (form is null) continue; // Must have form
      var normalizedForm = NormalizeForm(form);
      if (normalizedForm is null) continue;
      
      var karatValue = item.TryGetProperty("karat", out var karatProp) 
        ? karatProp.ValueKind == JsonValueKind.Number ? karatProp.GetInt32().ToString() 
          : karatProp.GetString()
        : item.TryGetProperty("purity", out var purityProp) 
          ? purityProp.GetString() 
          : "24";
      
      var karat = NormalizeKarat(karatValue ?? "24");

      var region = item.TryGetProperty("region", out var regionProp) ? regionProp.GetString() 
        : item.TryGetProperty("location", out var locProp) ? locProp.GetString() 
        : "Hanoi";

      var priceBuy = item.TryGetProperty("priceBuy", out var buyProp) ? buyProp.GetDecimal() 
        : item.TryGetProperty("buy", out var buyProp2) ? buyProp2.GetDecimal() 
        : 0;
      
      var priceSell = item.TryGetProperty("priceSell", out var sellProp) ? sellProp.GetDecimal() 
        : item.TryGetProperty("sell", out var sellProp2) ? sellProp2.GetDecimal() 
        : 0;

      if (priceBuy <= 0 || priceSell <= 0) continue;

      records.Add(new RawPriceRecord
      {
        SourceName = sourceName,
        Brand = "DOJI",
        Form = normalizedForm,
        Karat = karat,
        Region = NormalizeRegion(region ?? "Hanoi"),
        PriceBuy = priceBuy,
        PriceSell = priceSell,
        Currency = "VND",
        CollectedAt = now,
        EffectiveAt = now
      });
    }

    return records;
  }

  private static string? NormalizeForm(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return null;
    
    var lower = text.ToLowerInvariant();
    if (lower.Contains("nhẫn") || lower.Contains("ring"))
      return "ring";
    if (lower.Contains("miếng") || lower.Contains("bar") || lower.Contains("vàng miếng"))
      return "bar";
    return null;
  }

  private static string NormalizeKarat(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return "24";
    
    var lower = text.ToLowerInvariant();
    
    // Check for "9999" first (common synonym for 24K)
    if (lower.Contains("9999"))
      return "24";
    
    // Look for "24K", "18K", etc.
    var karatMatch = System.Text.RegularExpressions.Regex.Match(text, @"(\d{1,2})\s*K", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (karatMatch.Success && karatMatch.Groups[1].Success)
    {
      if (int.TryParse(karatMatch.Groups[1].Value, out var k) && k > 0 && k <= 24)
        return k.ToString();
    }
    
    // Look for numeric karat without K
    var numericMatch = System.Text.RegularExpressions.Regex.Match(text, @"\b(18|20|22|24)\b");
    if (numericMatch.Success)
    {
      if (int.TryParse(numericMatch.Value, out var k))
        return k.ToString();
    }
    
    return "24"; // Default
  }

  private static string NormalizeRegion(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return "Hanoi";
    
    var lower = text.ToLowerInvariant();
    if (lower.Contains("hanoi") || lower.Contains("hà nội"))
      return "Hanoi";
    if (lower.Contains("hcmc") || lower.Contains("hồ chí minh") || lower.Contains("ho chi minh") || lower.Contains("hcm"))
      return "HCMC";
    return "Hanoi"; // Default
  }

  private static decimal ParsePrice(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return 0;
    
    // Remove all non-digit characters except decimal point (though prices are integers)
    var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"[^\d]", "");
    
    if (decimal.TryParse(cleaned, out var price) && price > 0)
      return price;
    
    return 0;
  }

}

