using System.Globalization;
using System.Xml.Linq;
using GoldTracker.Domain.Normalization;

namespace GoldTracker.Infrastructure.Scrapers.Btmc;

public sealed class BtmcParser
{
  private static readonly TimeZoneInfo VietnamTimeZone =
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

  public IReadOnlyList<RawPriceRecord> Parse(string payload)
  {
    var records = new List<RawPriceRecord>();

    if (string.IsNullOrWhiteSpace(payload))
      return records;

    var trimmed = payload.TrimStart();

    // The BTMC endpoint returns JSON with attribute-like keys (e.g. "@n_1").
    // Try JSON first, then fall back to XML parsing.
    if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
    {
      try
      {
        using var doc = System.Text.Json.JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("DataList", out var dataList) &&
            dataList.TryGetProperty("Data", out var dataArray) &&
            dataArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
          foreach (var element in dataArray.EnumerateArray())
          {
            records.AddRange(ParseDataObject(element));
          }
        }

        if (records.Count > 0)
          return records;
      }
      catch
      {
        // Ignore and fall back to XML parsing below.
      }
    }

    try
    {
      var document = XDocument.Parse(payload);
      var dataElements = document.Descendants("Data");

      foreach (var data in dataElements)
      {
        var index = 1;
        while (true)
        {
          var nameAttr = data.Attribute($"n_{index}");
          if (nameAttr is null)
            break;

          var buyAttr = data.Attribute($"pb_{index}")?.Value;
          var sellAttr = data.Attribute($"ps_{index}")?.Value;
          var karatAttr = data.Attribute($"k_{index}")?.Value ?? data.Attribute($"h_{index}")?.Value;
          var dateAttr = data.Attribute($"d_{index}")?.Value;

          var priceBuy = ParsePrice(buyAttr);
          var priceSell = ParsePrice(sellAttr);

          if (priceBuy <= 0 || priceSell <= 0)
          {
            index++;
            continue;
          }

          var capturedAt = DateTimeOffset.UtcNow;
          var effectiveAt = (ParseTimestamp(dateAttr) ?? capturedAt).ToUniversalTime();

          records.Add(new RawPriceRecord
          {
            SourceName = "BTMC",
            Brand = "BTMC",
            Form = NormalizeForm(nameAttr.Value),
            Karat = NormalizeKarat(karatAttr),
            Region = "Hanoi",
            PriceBuy = priceBuy,
            PriceSell = priceSell,
            Currency = "VND",
            CollectedAt = capturedAt,
            EffectiveAt = effectiveAt
          });

          index++;
        }
      }
    }
    catch
    {
      // Invalid XML or unexpected schema - surface empty result, scraper handles error logging.
    }

    return records;
  }

  private static IEnumerable<RawPriceRecord> ParseDataObject(System.Text.Json.JsonElement element)
  {
    var records = new List<RawPriceRecord>();
    var collectedAt = DateTimeOffset.UtcNow;

    int? index = null;
    if (element.TryGetProperty("@row", out var rowProp))
    {
      var rowText = rowProp.GetString();
      if (int.TryParse(rowText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex))
        index = parsedIndex;
    }

    if (index is null)
    {
      // Fall back to detecting the first '@n_x' property.
      for (var probe = 1; probe < 128; probe++)
      {
        if (element.TryGetProperty($"@n_{probe}", out _))
        {
          index = probe;
          break;
        }
      }
    }

    if (index is null)
      return records;

    var name = element.TryGetProperty($"@n_{index}", out var nameProp) ? nameProp.GetString() : null;
    var priceBuy = GetDecimal(element, $"@pb_{index}");
    var priceSell = GetDecimal(element, $"@ps_{index}");

    if (priceBuy <= 0 || priceSell <= 0)
      return records;

    var karat = element.TryGetProperty($"@k_{index}", out var karatProp)
      ? karatProp.GetString()
      : element.TryGetProperty($"@h_{index}", out var purityProp) ? purityProp.GetString() : null;

    var effectiveAt = element.TryGetProperty($"@d_{index}", out var dateProp)
      ? (ParseTimestamp(dateProp.GetString())?.ToUniversalTime())
      : null;

    records.Add(new RawPriceRecord
    {
      SourceName = "BTMC",
      Brand = "BTMC",
      Form = NormalizeForm(name),
      Karat = NormalizeKarat(karat),
      Region = "Hanoi",
      PriceBuy = priceBuy,
      PriceSell = priceSell,
      Currency = "VND",
      CollectedAt = collectedAt,
      EffectiveAt = effectiveAt ?? collectedAt
    });

    return records;
  }

  private static decimal GetDecimal(System.Text.Json.JsonElement element, string propertyName)
  {
    if (!element.TryGetProperty(propertyName, out var prop))
      return 0;

    switch (prop.ValueKind)
    {
      case System.Text.Json.JsonValueKind.Number:
        return prop.TryGetDecimal(out var num) ? num : 0;
      case System.Text.Json.JsonValueKind.String:
        return ParsePrice(prop.GetString());
      default:
        return 0;
    }
  }

  private static decimal ParsePrice(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw))
      return 0;

    var cleaned = new string(raw.Where(char.IsDigit).ToArray());
    if (decimal.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var price))
      return price;
    return 0;
  }

  private static string NormalizeForm(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return "bar";

    var lower = text.ToLowerInvariant();
    if (lower.Contains("nhẫn") || lower.Contains("nhan") || lower.Contains("ring"))
      return "ring";
    if (lower.Contains("trang sức") || lower.Contains("trang suc") || lower.Contains("jewelry"))
      return "jewelry";
    if (lower.Contains("quà mừng") || lower.Contains("qua mung"))
      return "gift";
    return "bar";
  }

  private static string NormalizeKarat(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return "24";

    var cleaned = text.Trim().ToLowerInvariant();
    if (cleaned.Contains("9999") || cleaned.Contains("999.9"))
      return "24";

    var match = System.Text.RegularExpressions.Regex.Match(cleaned, @"(\d{1,2})");
    if (match.Success && int.TryParse(match.Value, out var value))
      return value.ToString(CultureInfo.InvariantCulture);

    return "24";
  }

  private static DateTimeOffset? ParseTimestamp(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return null;

    var formats = new[] { "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm" };
    if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
    {
      var offset = VietnamTimeZone.GetUtcOffset(dt);
      return new DateTimeOffset(dt, offset);
    }

    return null;
  }
}

