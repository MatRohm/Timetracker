using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Timetracker.Plugins.Contracts.Logging;

namespace Timetracker.App.Tests.Unit.Logging;

[TestFixture]
public sealed class FileLoggingTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "tt-logging-tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void AddTimetrackerFile_WhenAnErrorIsLogged_ShouldWriteItToTheProcessesDailyFile()
    {
        using (var factory = LoggerFactory.Create(builder => builder.AddTimetrackerFile("app", _directory)))
        {
            factory.CreateLogger<FileLoggingTests>()
                .LogError(new IOException("disk full"), "Could not save {Task}", "Report");
        }

        var file = Directory.GetFiles(_directory).Should().ContainSingle().Which;
        Path.GetFileName(file).Should().Be($"app-{DateTime.Now:yyyyMMdd}.log");
        var text = File.ReadAllText(file);
        text.Should().Contain("[ERR]");
        text.Should().Contain(typeof(FileLoggingTests).FullName);
        text.Should().Contain("Could not save Report");
        text.Should().Contain("System.IO.IOException: disk full");
    }

    [Test]
    public void AddTimetrackerFile_WhenNothingCreatesTheFactory_ShouldNotCreateTheFolder()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddTimetrackerFile("app", _directory));

        Directory.Exists(_directory).Should().BeFalse("the file is only opened once logging is used");
    }
}
