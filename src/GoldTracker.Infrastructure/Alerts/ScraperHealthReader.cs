using System.Net.Http;
using System.Text.Json;
using Dapper;
using GoldTracker.Application.Contracts.Alerts;
using GoldTracker.Infrastructure.Config;
using GoldTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldTracker.Infrastructure.Alerts;

public sealed class ScraperHealthReader : IScraperHealthReader
{
  private readonly HttpClient _httpClient;
  private readonly ElasticsearchOptions _options;
  private readonly DapperConnectionFactory _factory;
  private readonly ILogger<ScraperHealthReader> _logger;

  public ScraperHealthReader(
    IHttpClientFactory httpClientFactory,
    IOptions<ElasticsearchOptions> options,
    DapperConnectionFactory factory,
    ILogger<ScraperHealthReader> logger)
  {
    _httpClient = httpClientFactory.CreateClient("elasticsearch");
    _options = options.Value;
    _factory = factory;
    _logger = logger;
  }

  public async Task<bool> HasErrorsAsync(TimeSpan within, CancellationToken ct = default)
  {
    if (!_options.Enabled)
      return false;

    try
    {
      var cutoff = DateTimeOffset.UtcNow - within;
      var query = new Dictionary<string, object>
      {
        ["query"] = new Dictionary<string, object>
        {
          ["bool"] = new Dictionary<string, object>
          {
            ["must"] = new object[]
            {
              new Dictionary<string, object> { ["range"] = new Dictionary<string, object> { ["@timestamp"] = new Dictionary<string, object> { ["gte"] = cutoff.ToString("O") } } },
              // ECS convention is lowercase ("error"). Keep compatibility with older docs that might have "Error".
              new Dictionary<string, object> { ["terms"] = new Dictionary<string, object> { ["log.level"] = new[] { "error", "Error" } } },
              new Dictionary<string, object> { ["wildcard"] = new Dictionary<string, object> { ["SourceContext"] = "*Scraper*" } }
            }
          }
        },
        ["size"] = 1
      };

      var json = JsonSerializer.Serialize(query);
      var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
      var url = $"{_options.BaseUrl.TrimEnd('/')}/{_options.IndexLogs}/_search";
      
      var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
      if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
      {
        var auth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
      }

      var response = await _httpClient.SendAsync(request, ct);
      if (!response.IsSuccessStatusCode)
      {
        _logger.LogWarning("Elasticsearch query failed: {StatusCode}", response.StatusCode);
        return false;
      }

      var responseJson = await response.Content.ReadAsStringAsync(ct);
      var doc = JsonDocument.Parse(responseJson);
      var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");
      return hits.GetArrayLength() > 0;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to check scraper errors from Elasticsearch");
      return false;
    }
  }

  public async Task<bool> HasNoDataAsync(TimeSpan within, CancellationToken ct = default)
  {
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var cutoff = DateTimeOffset.UtcNow - within;
    var count = await conn.QuerySingleAsync<int>(
      @"SELECT COUNT(*) FROM gold.price_tick WHERE effective_at >= @Cutoff",
      new { Cutoff = cutoff });
    
    return count == 0;
  }
}

