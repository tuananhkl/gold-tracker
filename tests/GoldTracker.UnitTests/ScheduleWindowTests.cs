using FluentAssertions;
using GoldTracker.Infrastructure.Scheduling;
using Xunit;

namespace GoldTracker.UnitTests;

public class ScheduleWindowTests
{
  [Theory]
  [InlineData("07:29", false)]
  [InlineData("07:30", true)]
  [InlineData("12:00", true)]
  [InlineData("21:00", true)]
  [InlineData("21:01", false)]
  public void IsInWindow_should_respect_boundaries(string timeStr, bool expected)
  {
    var time = TimeOnly.Parse(timeStr);
    var start = new TimeOnly(7, 30);
    var end = new TimeOnly(21, 0);

    var result = TimeWindow.IsInWindow(time, start, end);

    result.Should().Be(expected);
  }

  [Fact]
  public void ParseTime_should_parse_valid_time()
  {
    var result = TimeWindow.ParseTime("09:15", new TimeOnly(7, 30));

    result.Should().Be(new TimeOnly(9, 15));
  }

  [Fact]
  public void ParseTime_should_use_default_for_invalid()
  {
    var defaultValue = new TimeOnly(7, 30);
    var result = TimeWindow.ParseTime("invalid", defaultValue);

    result.Should().Be(defaultValue);
  }

  [Fact]
  public void ParseTime_should_use_default_for_null()
  {
    var defaultValue = new TimeOnly(7, 30);
    var result = TimeWindow.ParseTime(null!, defaultValue);

    result.Should().Be(defaultValue);
  }
}

