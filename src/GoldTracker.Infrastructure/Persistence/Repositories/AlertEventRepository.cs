using Dapper;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Domain.Alerts;
using GoldTracker.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GoldTracker.Infrastructure.Persistence.Repositories;

public sealed class AlertEventRepository : IAlertEventRepository
{
  private readonly DapperConnectionFactory _factory;

  public AlertEventRepository(DapperConnectionFactory factory)
  {
    _factory = factory;
  }

  public async Task AddAsync(AlertEvent alert, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    await conn.ExecuteAsync(
      @"INSERT INTO gold.alert_event (id, created_at, kind, brand, region, product_kind, message, severity, meta, dedup_key)
        VALUES (@Id, @CreatedAt, @Kind, @Brand, @Region, @ProductKind, @Message, @Severity, @Meta::jsonb, @DedupKey)",
      new
      {
        alert.Id,
        alert.CreatedAt,
        alert.Kind,
        alert.Brand,
        alert.Region,
        ProductKind = alert.ProductKind,
        alert.Message,
        alert.Severity,
        Meta = JsonSerializer.Serialize(alert.Meta),
        alert.DedupKey
      });
  }

  public async Task<AlertEvent?> GetLastByDedupKeyAsync(string dedupKey, TimeSpan window, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    var cutoff = DateTimeOffset.UtcNow - window;
    var result = await conn.QueryFirstOrDefaultAsync<dynamic>(
      @"SELECT id, created_at, kind, brand, region, product_kind, message, severity, meta, dedup_key
        FROM gold.alert_event
        WHERE dedup_key = @DedupKey AND created_at >= @Cutoff
        ORDER BY created_at DESC
        LIMIT 1",
      new { DedupKey = dedupKey, Cutoff = cutoff });
    
    if (result == null)
      return null;

    var metaDict = new Dictionary<string, object>();
    if (result.meta != null)
    {
      var metaJson = result.meta.ToString();
      if (!string.IsNullOrEmpty(metaJson))
      {
        var doc = JsonDocument.Parse(metaJson);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
          metaDict[prop.Name] = prop.Value.GetRawText();
        }
      }
    }

    return new AlertEvent
    {
      Id = result.id,
      CreatedAt = result.created_at,
      Kind = result.kind,
      Brand = result.brand,
      Region = result.region,
      ProductKind = result.product_kind,
      Message = result.message,
      Severity = result.severity,
      Meta = metaDict,
      DedupKey = result.dedup_key
    };
  }
}

