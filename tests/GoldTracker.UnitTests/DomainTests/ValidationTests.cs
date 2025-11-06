using FluentAssertions;
using GoldTracker.Application.Services;
using Xunit;

namespace GoldTracker.UnitTests.DomainTests;

public class ValidationTests
{
  [Theory]
  [InlineData(7)]
  [InlineData(30)]
  [InlineData(90)]
  [InlineData(180)]
  public async Task History_days_allowed(int days)
  {
    var svc = new InMemoryPriceService();
    var (_, _, points) = await svc.GetHistoryAsync("ring", days, null, null);
    points.Should().NotBeNull();
  }
}
