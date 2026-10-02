using AwesomeAssertions;
using NUnit.Framework;

namespace Timetracker.Plugins.ActivityMonitor.Tests.Unit;

/// <summary>
/// The idle threshold options: values are whole minutes; missing, blank,
/// non-numeric or non-positive values fall back to the built-in defaults.
/// </span>
[TestFixture]
public sealed class IdleOptionsTests
{
    [Test]
    public void FromMinutes_WhenValueIsMissing_ShouldFallBack()
    {
        IdleOptions.FromMinutes(null, TimeSpan.FromMinutes(45))
            .Should().Be(TimeSpan.FromMinutes(45), "there is no stored value");
    }

    [Test]
    public void FromMinutes_WhenValueIsBlank_ShouldFallBack()
    {
        IdleOptions.FromMinutes("   ", TimeSpan.FromMinutes(45))
            .Should().Be(TimeSpan.FromMinutes(45), "blank values carry no information");
    }

    [Test]
    public void FromMinutes_WhenValueIsNonNumeric_ShouldFallBack()
    {
        IdleOptions.FromMinutes("soon", TimeSpan.FromMinutes(45))
            .Should().Be(TimeSpan.FromMinutes(45), "only whole minutes are accepted");
    }

    [Test]
    public void FromMinutes_WhenValueIsZeroOrNegative_ShouldFallBack()
    {
        IdleOptions.FromMinutes("0", TimeSpan.FromMinutes(45))
            .Should().Be(TimeSpan.FromMinutes(45), "a zero threshold would log every tiny break");
        IdleOptions.FromMinutes("-5", TimeSpan.FromMinutes(45))
            .Should().Be(TimeSpan.FromMinutes(45));
    }

    [Test]
    public void FromMinutes_WhenValueIsValidMinutes_ShouldReturnTheDuration()
    {
        IdleOptions.FromMinutes("90", TimeSpan.FromMinutes(60))
            .Should().Be(TimeSpan.FromMinutes(90));
        IdleOptions.FromMinutes(" 30 ", TimeSpan.FromMinutes(60))
            .Should().Be(TimeSpan.FromMinutes(30), "int.TryParse tolerates surrounding whitespace");
    }

    [Test]
    public void FromMinutes_WhenValueHasFractionsOrLocaleFormats_ShouldFallBack()
    {
        IdleOptions.FromMinutes("45.5", TimeSpan.FromMinutes(60))
            .Should().Be(TimeSpan.FromMinutes(60), "only whole minutes are accepted");
        IdleOptions.FromMinutes("1,5", TimeSpan.FromMinutes(60))
            .Should().Be(TimeSpan.FromMinutes(60));
    }

    [Test]
    public void IdleSpanThreshold_WhenValueIsMissing_ShouldFallBackToOneHour()
    {
        IdleOptions.IdleSpanThreshold(null).Should().Be(TimeSpan.FromHours(1));
    }

    [Test]
    public void IdleStopThreshold_WhenValueIsMissing_ShouldFallBackToThirtyMinutes()
    {
        IdleOptions.IdleStopThreshold(null).Should().Be(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void ReadValueFromOptionsFile_WhenFileIsMissing_ShouldReturnNull()
    {
        var result = IdleOptions.ReadValueFromOptionsFile(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"),
            IdleOptions.IdleSpanThresholdKey);

        result.Should().BeNull("a missing file yields no options");
    }

    [Test]
    public void ReadValueFromOptionsFile_WhenFileIsBroken_ShouldReturnNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            var result = IdleOptions.ReadValueFromOptionsFile(path, IdleOptions.IdleSpanThresholdKey);

            result.Should().BeNull("a broken file yields no options");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ReadValueFromOptionsFile_WhenKeyIsStored_ShouldReturnTheValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"options-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"ActivityMonitor.IdleSpanThreshold": "90"}""");
        try
        {
            var result = IdleOptions.ReadValueFromOptionsFile(path, IdleOptions.IdleSpanThresholdKey);

            result.Should().Be("90");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
