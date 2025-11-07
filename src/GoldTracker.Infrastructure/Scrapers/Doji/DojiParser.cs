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

    // Try to find price tables or price elements
    // This is a generic parser - adjust selectors based on actual DOJI HTML structure
    var priceRows = document.QuerySelectorAll("tr, .price-row, .gold-item, [data-type='gold']");
    
    foreach (var row in priceRows)
    {
      var text = row.TextContent ?? string.Empty;
      
      // Look for ring/bar indicators
      var form = ExtractForm(text);
      if (form is null) continue;

      // Extract karat
      var karat = ExtractKarat(text);

      // Extract region
      var region = ExtractRegion(text);

      // Extract prices (look for numeric patterns)
      var prices = ExtractPrices(text);
      if (prices.buy <= 0 || prices.sell <= 0) continue;

      records.Add(new RawPriceRecord
      {
        SourceName = sourceName,
        Brand = "DOJI",
        Form = form,
        Karat = karat?.ToString(),
        Region = region,
        PriceBuy = prices.buy,
        PriceSell = prices.sell,
        Currency = "VND",
        CollectedAt = now,
        EffectiveAt = now
      });
    }

    // If no rows found, try alternative parsing (JSON or different HTML structure)
    if (records.Count == 0)
    {
      // Fallback: try to parse as JSON
      try
      {
        var jsonRecords = ParseJson(html, sourceName);
        if (jsonRecords.Count > 0)
          return jsonRecords;
      }
      catch
      {
        // Not JSON, continue
      }

      // Last resort: create a default ring entry if we can extract at least prices
      var allPrices = ExtractPrices(html);
      if (allPrices.buy > 0 && allPrices.sell > 0)
      {
        records.Add(new RawPriceRecord
        {
          SourceName = sourceName,
          Brand = "DOJI",
          Form = "ring",
          Karat = "24",
          Region = "Hanoi",
          PriceBuy = allPrices.buy,
          PriceSell = allPrices.sell,
          Currency = "VND",
          CollectedAt = now,
          EffectiveAt = now
        });
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
        : "ring";
      
      var karat = item.TryGetProperty("karat", out var karatProp) ? karatProp.GetInt32() 
        : item.TryGetProperty("purity", out var purityProp) ? ParseKaratFromString(purityProp.GetString()) 
        : 24;

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
        Form = form ?? "ring",
        Karat = karat?.ToString(),
        Region = NormalizeRegion(region),
        PriceBuy = priceBuy,
        PriceSell = priceSell,
        Currency = "VND",
        CollectedAt = now,
        EffectiveAt = now
      });
    }

    return records;
  }

  private static string? ExtractForm(string text)
  {
    var lower = text.ToLowerInvariant();
    if (lower.Contains("nhẫn") || lower.Contains("ring"))
      return "ring";
    if (lower.Contains("miếng") || lower.Contains("bar") || lower.Contains("vàng miếng"))
      return "bar";
    return null;
  }

  private static int? ExtractKarat(string text)
  {
    // Look for "24K", "9999", "24", etc.
    var karatMatch = System.Text.RegularExpressions.Regex.Match(text, @"(\d{1,2})\s*K|9999|(\d{1,2})\s*karat");
    if (karatMatch.Success)
    {
      if (karatMatch.Groups[1].Success && int.TryParse(karatMatch.Groups[1].Value, out var k))
        return k;
      if (text.Contains("9999"))
        return 24;
    }
    return 24; // Default
  }

  private static string? ExtractRegion(string text)
  {
    var lower = text.ToLowerInvariant();
    if (lower.Contains("hanoi") || lower.Contains("hà nội"))
      return "Hanoi";
    if (lower.Contains("hcmc") || lower.Contains("hồ chí minh") || lower.Contains("ho chi minh"))
      return "HCMC";
    return "Hanoi"; // Default
  }

  private static (decimal buy, decimal sell) ExtractPrices(string text)
  {
    // Remove dots, commas, and extract numbers
    var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"[^\d]", " ");
    var numbers = System.Text.RegularExpressions.Regex.Matches(cleaned, @"\d{6,}")
      .Cast<System.Text.RegularExpressions.Match>()
      .Select(m => decimal.TryParse(m.Value, out var v) ? v : 0)
      .Where(v => v > 1000000) // Prices should be > 1M VND
      .ToList();

    if (numbers.Count >= 2)
      return (numbers[0], numbers[1]);
    if (numbers.Count == 1)
      return (numbers[0], numbers[0] + 100000); // Estimate sell = buy + 100k

    return (0, 0);
  }

  private static int? ParseKaratFromString(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return 24;
    
    if (value.Contains("9999") || value.Contains("999"))
      return 24;
    
    var match = System.Text.RegularExpressions.Regex.Match(value, @"(\d{1,2})");
    if (match.Success && int.TryParse(match.Groups[1].Value, out var k))
      return k;
    
    return 24;
  }

  private static string NormalizeRegion(string? region)
  {
    if (string.IsNullOrWhiteSpace(region))
      return "Hanoi";
    
    var lower = region.ToLowerInvariant();
    if (lower.Contains("hanoi") || lower.Contains("hà nội"))
      return "Hanoi";
    if (lower.Contains("hcmc") || lower.Contains("hồ chí minh") || lower.Contains("ho chi minh") || lower.Contains("hcm"))
      return "HCMC";
    
    return region;
  }
}

