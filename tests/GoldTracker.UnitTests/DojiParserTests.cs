using FluentAssertions;
using GoldTracker.Domain.Normalization;
using GoldTracker.Infrastructure.Scrapers.Doji;
using Xunit;

namespace GoldTracker.UnitTests;

public class DojiParserTests
{
  private readonly DojiParser _parser = new();

  [Fact]
  public void ParseHtml_should_extract_ring_prices()
  {
    var html = @"
      <html>
        <body>
          <table>
            <tr>
              <td>Nhẫn tròn trơn 24K</td>
              <td>Hanoi</td>
              <td>7,420,000</td>
              <td>7,520,000</td>
            </tr>
          </table>
        </body>
      </html>";

    var records = _parser.ParseHtml(html, "DOJI");

    records.Should().NotBeEmpty();
    var record = records.First();
    record.SourceName.Should().Be("DOJI");
    record.Brand.Should().Be("DOJI");
    record.Form.Should().Be("ring");
    record.Karat.Should().Be("24");
    record.Region.Should().Be("Hanoi");
    record.PriceBuy.Should().HaveValue().And.BeGreaterThan(7000000m);
    record.PriceSell.Should().HaveValue().And.BeGreaterThan(record.PriceBuy!.Value);
    record.Currency.Should().Be("VND");
  }

  [Fact]
  public void ParseHtml_should_extract_bar_prices()
  {
    var html = @"
      <html>
        <body>
          <div class=""price-row"">
            <span>Vàng miếng 9999</span>
            <span>HCMC</span>
            <span>7,500,000</span>
            <span>7,600,000</span>
          </div>
        </body>
      </html>";

    var records = _parser.ParseHtml(html, "DOJI");

    records.Should().NotBeEmpty();
    var record = records.First();
    record.Form.Should().Be("bar");
    record.Karat.Should().Be("24");
    record.Region.Should().Be("HCMC");
  }

  [Fact]
  public void ParseJson_should_extract_prices()
  {
    var json = @"{
      ""data"": [
        {
          ""form"": ""ring"",
          ""karat"": 24,
          ""region"": ""Hanoi"",
          ""priceBuy"": 7420000,
          ""priceSell"": 7520000
        }
      ]
    }";

    var records = _parser.ParseJson(json, "DOJI");

    records.Should().ContainSingle();
    var record = records.First();
    record.SourceName.Should().Be("DOJI");
    record.Brand.Should().Be("DOJI");
    record.Form.Should().Be("ring");
    record.Karat.Should().Be("24");
    record.Region.Should().Be("Hanoi");
    record.PriceBuy.Should().Be(7420000);
    record.PriceSell.Should().Be(7520000);
    record.Currency.Should().Be("VND");
  }

  [Fact]
  public void ParseJson_should_handle_array_format()
  {
    var json = @"[
      {
        ""type"": ""bar"",
        ""purity"": ""9999"",
        ""location"": ""HCMC"",
        ""buy"": 7500000,
        ""sell"": 7600000
      }
    ]";

    var records = _parser.ParseJson(json, "DOJI");

    records.Should().ContainSingle();
    var record = records.First();
    record.Form.Should().Be("bar");
    record.Karat.Should().Be("24");
    record.Region.Should().Be("HCMC");
  }
}

