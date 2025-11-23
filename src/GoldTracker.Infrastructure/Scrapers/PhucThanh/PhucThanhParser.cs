using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using GoldTracker.Domain.Normalization;
using Microsoft.Extensions.Logging;

namespace GoldTracker.Infrastructure.Scrapers.PhucThanh;

public sealed class PhucThanhParser
{
  private readonly ILogger<PhucThanhParser>? _logger;
  
  // Mapping from text in "Loại vàng" column to normalized form/karat
  private static readonly (string searchText, string form, string karat)[] Targets =
  {
    ("Nhẫn tròn 9999", "ring", "24"),
    ("Nhẫn tròn", "ring", "24"),
    ("Trang sức 9999", "jewelry", "24"),
    ("Trang sức 999", "jewelry", "24"),
    ("Trang sức 99%", "jewelry", "24"),
    ("Trang sức", "jewelry", "24"),
  };

  private static readonly Regex DateRegex = new(@"(\d{2}:\d{2}).*?(\d{1,2}/\d{1,2}/\d{4})", RegexOptions.Compiled);

  public PhucThanhParser(ILogger<PhucThanhParser>? logger = null)
  {
    _logger = logger;
  }

  public IReadOnlyList<RawPriceRecord> Parse(string html)
  {
    var results = new List<RawPriceRecord>();
    if (string.IsNullOrWhiteSpace(html))
    {
      _logger?.LogWarning("Empty HTML input");
      return results;
    }

    try
    {
      var context = BrowsingContext.New(Configuration.Default);
      var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

      // Step 1: Find heading "BẢNG TỶ GIÁ VÀNG"
      var headingNode = FindHeadingNode(document);
      if (headingNode == null)
      {
        _logger?.LogWarning("Could not find heading 'BẢNG TỶ GIÁ VÀNG'");
        return results;
      }

      // Step 2: Find table after heading
      var table = FindTableFromHeading(headingNode);
      if (table == null)
      {
        _logger?.LogError("Found heading but could not find table");
        return results;
      }

      // Step 3: Parse table rows
      var tableRecords = ParseGoldPriceTable(table);
      if (tableRecords.Count == 0)
      {
        _logger?.LogError("Found table but no valid rows parsed");
        return results;
      }

      // Step 4: Parse metadata below table (unit and update time)
      var (unitText, effectiveAt) = ParseMetadataBelowTable(table);

      // Step 5: Create RawPriceRecord for each parsed row
      var now = DateTimeOffset.UtcNow;
      foreach (var (form, karat, priceBuy, priceSell) in tableRecords)
      {
        // Site unit: prices are per "chỉ" (1 chỉ = 1/10 cây)
        // Convert to VND per cây (10 chỉ)
        var buyVnd = priceBuy * 1000m * 10m;
        var sellVnd = priceSell * 1000m * 10m;

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
          CollectedAt = now,
          EffectiveAt = effectiveAt ?? now
        });
      }

      _logger?.LogInformation("Parsed {Count} PhucThanh records. Unit: {Unit}, EffectiveAt: {EffectiveAt}", 
        results.Count, unitText ?? "N/A", effectiveAt?.ToString("yyyy-MM-dd HH:mm") ?? "N/A");

      return results;
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Error parsing PhucThanh HTML");
      return results;
    }
  }

  /// <summary>
  /// Find heading node containing "BẢNG TỶ GIÁ VÀNG" using text-based traversal.
  /// Searches h2, h3, and div elements.
  /// </summary>
  private IElement? FindHeadingNode(IDocument document)
  {
    // Try full text first, then fallback to partial match
    var targetTexts = new[] { "BẢNG TỶ GIÁ VÀNG", "BẢNG TỶ GIÁ" };
    
    // Search in h2, h3, and div elements
    var candidates = document.Descendants()
      .Where(n => n is IElement el && 
                  (el.TagName.Equals("H2", StringComparison.OrdinalIgnoreCase) ||
                   el.TagName.Equals("H3", StringComparison.OrdinalIgnoreCase) ||
                   el.TagName.Equals("DIV", StringComparison.OrdinalIgnoreCase)))
      .Cast<IElement>();

    foreach (var element in candidates)
    {
      var normalized = NormalizeText(element.TextContent);
      foreach (var targetText in targetTexts)
      {
        if (normalized.Contains(targetText, StringComparison.OrdinalIgnoreCase))
        {
          _logger?.LogInformation("Found heading node: {TagName}, Text: {Text}, Matched: {Target}", 
            element.TagName, element.TextContent?.Trim(), targetText);
          return element;
        }
      }
    }

    _logger?.LogWarning("Could not find heading. Searched {Count} candidate elements", candidates.Count());
    return null;
  }

  /// <summary>
  /// Find table element immediately after the heading node.
  /// First tries NextSibling, then searches in ParentNode.Descendants.
  /// </summary>
  private IElement? FindTableFromHeading(IElement headingNode)
  {
    // Try NextSibling first
    var next = headingNode.NextSibling;
    var siblingCount = 0;
    while (next != null && siblingCount < 20) // Limit search to avoid infinite loops
    {
      siblingCount++;
      if (next is IElement el && el.TagName.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
      {
        _logger?.LogInformation("Found table via NextSibling (checked {Count} siblings)", siblingCount);
        return el;
      }
      next = next.NextSibling;
    }

    // If not found, check if heading is inside a div/section that contains a table
    var parent = headingNode.ParentElement;
    if (parent != null)
    {
      var allTables = parent.Descendants()
        .Where(n => n is IElement el && el.TagName.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
        .Cast<IElement>()
        .ToList();
      
      _logger?.LogDebug("Found {Count} tables in parent. Parent tag: {Tag}", allTables.Count, parent.TagName);
      
      if (allTables.Count > 0)
      {
        // Prefer the first table that comes after the heading in document order
        var table = allTables.FirstOrDefault();
        if (table != null)
        {
          _logger?.LogInformation("Found table via ParentNode.Descendants");
          return table;
        }
      }
    }

    _logger?.LogWarning("Could not find table after heading. Checked {SiblingCount} siblings, parent: {ParentTag}", 
      siblingCount, parent?.TagName ?? "null");
    return null;
  }

  /// <summary>
  /// Parse gold price table. Skips header row. Each row must have >= 3 cells.
  /// Cell[0] = Loại vàng, Cell[1] = BÁN RA, Cell[2] = MUA VÀO
  /// </summary>
  private List<(string form, string karat, decimal priceBuy, decimal priceSell)> ParseGoldPriceTable(IElement table)
  {
    var results = new List<(string form, string karat, decimal priceBuy, decimal priceSell)>();
    
    var rows = table.Descendants()
      .Where(n => n is IElement el && el.TagName.Equals("TR", StringComparison.OrdinalIgnoreCase))
      .Cast<IElement>()
      .ToList();

    _logger?.LogInformation("Found {Count} total rows in table", rows.Count);

    if (rows.Count == 0)
    {
      _logger?.LogWarning("Table has no rows");
      return results;
    }

    // Log first row (header) for debugging
    if (rows.Count > 0)
    {
      var headerCells = rows[0].Descendants()
        .Where(n => n is IElement el && (el.TagName.Equals("TD", StringComparison.OrdinalIgnoreCase) || 
                                        el.TagName.Equals("TH", StringComparison.OrdinalIgnoreCase)))
        .Cast<IElement>()
        .Select(c => c.TextContent?.Trim() ?? "")
        .ToList();
      _logger?.LogDebug("Header row has {Count} cells: {Cells}", headerCells.Count, string.Join(" | ", headerCells));
    }

    // Skip first row (header)
    var dataRows = rows.Skip(1).ToList();
    _logger?.LogInformation("Processing {Count} data rows (skipped header)", dataRows.Count);

    foreach (var row in dataRows)
    {
      var cells = row.Descendants()
        .Where(n => n is IElement el && el.TagName.Equals("TD", StringComparison.OrdinalIgnoreCase))
        .Cast<IElement>()
        .ToList();

      if (cells.Count < 3)
      {
        _logger?.LogDebug("Row has less than 3 cells, skipping. Cell count: {Count}", cells.Count);
        continue;
      }

      // Cell[0]: Loại vàng
      var loaiVangText = cells[0].TextContent?.Trim() ?? string.Empty;
      var (form, karat) = MapFormAndKarat(loaiVangText);
      if (form == null)
      {
        _logger?.LogDebug("Could not map form/karat for text: {Text}", loaiVangText);
        continue;
      }

      // Cell[1]: BÁN RA (sell price)
      var banRaText = cells[1].TextContent?.Trim() ?? string.Empty;
      var priceSell = ParsePrice(banRaText);
      if (priceSell <= 0)
      {
        _logger?.LogDebug("Invalid sell price for text: {Text}", banRaText);
        continue;
      }

      // Cell[2]: MUA VÀO (buy price)
      var muaVaoText = cells[2].TextContent?.Trim() ?? string.Empty;
      var priceBuy = ParsePrice(muaVaoText);
      if (priceBuy <= 0)
      {
        _logger?.LogDebug("Invalid buy price for text: {Text}", muaVaoText);
        continue;
      }

      results.Add((form, karat, priceBuy, priceSell));
      _logger?.LogDebug("Parsed row: {Form}/{Karat}, Buy: {Buy}, Sell: {Sell}", form, karat, priceBuy, priceSell);
    }

    return results;
  }

  /// <summary>
  /// Parse metadata below table: unit text and update time.
  /// Looks for <p> elements containing "Đơn vị tính" and "Cập nhật lúc".
  /// </summary>
  private (string? unitText, DateTimeOffset? effectiveAt) ParseMetadataBelowTable(IElement table)
  {
    string? unitText = null;
    DateTimeOffset? effectiveAt = null;

    // Find parent container of table
    var container = table.ParentElement;
    if (container == null) return (unitText, effectiveAt);

    // Look for <p> elements after the table
    var paragraphs = container.Descendants()
      .Where(n => n is IElement el && el.TagName.Equals("P", StringComparison.OrdinalIgnoreCase))
      .Cast<IElement>()
      .ToList();

    foreach (var p in paragraphs)
    {
      var text = p.TextContent?.Trim() ?? string.Empty;
      var normalized = NormalizeText(text);

      // Check for "Đơn vị tính"
      if (normalized.Contains("ĐƠN VỊ TÍNH", StringComparison.OrdinalIgnoreCase) ||
          normalized.Contains("DON VI TINH", StringComparison.OrdinalIgnoreCase))
      {
        unitText = text;
        _logger?.LogDebug("Found unit text: {Text}", unitText);
      }

      // Check for "Cập nhật lúc"
      if (normalized.Contains("CẬP NHẬT LÚC", StringComparison.OrdinalIgnoreCase) ||
          normalized.Contains("CAP NHAT LUC", StringComparison.OrdinalIgnoreCase))
      {
        var match = DateRegex.Match(text);
        if (match.Success && match.Groups.Count >= 3)
        {
          var timeStr = match.Groups[1].Value; // e.g., "13:59"
          var dateStr = match.Groups[2].Value; // e.g., "23/11/2025"

          try
          {
            var dateTimeStr = $"{timeStr} {dateStr}";
            // Parse as local time (Vietnam timezone +07:00), then convert to UTC
            var localTime = DateTimeOffset.ParseExact(dateTimeStr, "HH:mm dd/MM/yyyy", 
              CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
            effectiveAt = localTime.ToUniversalTime();
            _logger?.LogDebug("Parsed effective date: {Date} (UTC: {UtcDate})", localTime, effectiveAt);
          }
          catch (Exception ex)
          {
            _logger?.LogWarning(ex, "Failed to parse date from text: {Text}", text);
          }
        }
      }
    }

    return (unitText, effectiveAt);
  }

  /// <summary>
  /// Map text from "Loại vàng" column to normalized form and karat.
  /// </summary>
  private (string? form, string karat) MapFormAndKarat(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return (null, "24");

    var normalized = NormalizeText(text);

    foreach (var (searchText, form, karat) in Targets)
    {
      var normalizedSearch = NormalizeText(searchText);
      if (normalized.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
      {
        return (form, karat);
      }
    }

    // Fallback: try to detect form from keywords
    if (normalized.Contains("NHẪN", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("NHAN", StringComparison.OrdinalIgnoreCase))
    {
      return ("ring", "24");
    }

    if (normalized.Contains("TRANG SỨC", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("TRANG SUC", StringComparison.OrdinalIgnoreCase))
    {
      return ("jewelry", "24");
    }

    return (null, "24");
  }

  /// <summary>
  /// Parse price text. Removes dots and commas, then parses as integer.
  /// Examples: "14,350" -> 14350, "14350" -> 14350
  /// </summary>
  private decimal ParsePrice(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return 0;

    var clean = text.Replace(".", "").Replace(",", "").Trim();
    if (int.TryParse(clean, out var value) && value > 0)
    {
      return value;
    }

    return 0;
  }

  /// <summary>
  /// Normalize text: trim, remove multiple spaces, convert to uppercase.
  /// </summary>
  private static string NormalizeText(string? text)
  {
    if (string.IsNullOrWhiteSpace(text)) return string.Empty;
    
    var trimmed = text.Trim();
    var noMultipleSpaces = Regex.Replace(trimmed, @"\s+", " ");
    return noMultipleSpaces.ToUpperInvariant();
  }
}
