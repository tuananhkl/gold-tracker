using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GoldTracker.ApiTests;

public class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
  private readonly WebApplicationFactory<Program> _factory;
  public ApiSmokeTests(WebApplicationFactory<Program> factory) => _factory = factory;

  [Theory]
  [InlineData("/healthz")]
  [InlineData("/readyz")]
  public async Task Ops_endpoints_return_200(string path)
  {
    var client = _factory.CreateClient();
    var res = await client.GetAsync(path);
    res.StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Theory]
  [InlineData("/api/prices/latest?kind=ring")]
  [InlineData("/api/prices/history?kind=ring&days=30")]
  [InlineData("/api/prices/changes?kind=ring")]
  [InlineData("/api/sources/health")]
  public async Task Api_endpoints_return_200_and_shape(string path)
  {
    var client = _factory.CreateClient();
    var res = await client.GetAsync(path);
    res.StatusCode.Should().Be(HttpStatusCode.OK);
    var json = await res.Content.ReadAsStringAsync();
    json.Should().NotBeNullOrWhiteSpace();

    using var doc = JsonDocument.Parse(json);
    doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
  }
}
