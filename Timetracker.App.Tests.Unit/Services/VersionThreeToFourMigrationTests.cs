using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Services;

namespace Timetracker.App.Tests.Unit.Services;

/// <summary>The version-3 step simplifies sessions to start plus seconds and gives tasks ids.</summary>
[TestFixture]
public sealed class VersionThreeToFourMigrationTests
{
    private const string SessionId = "11111111-1111-1111-1111-111111111111";

    [Test]
    public void FromVersion_WhenAsked_ShouldStateTheRangeItHandles()
    {
        var migration = new VersionThreeToFourMigration("unused.json");

        migration.FromVersion.Should().Be(3);
        migration.ToVersion.Should().Be(4);
        migration.ToVersion.Should().Be(migration.FromVersion + 1, "a step moves exactly one version");
        migration.BackupPath.Should().Be("unused.json.v3-backup");
    }

    [Test]
    public void Read_WhenGivenAVersionThreeFile_ShouldSimplifySessionsToStartPlusSeconds()
    {
        var migration = new VersionThreeToFourMigration("unused.json");
        var output = migration.Read(VersionThree(Session("2026-09-18T09:00:00+02:00", "2026-09-18T10:30:00+02:00")));

        var json = Parse(output);
        json.RootElement.GetProperty("version").GetInt32().Should().Be(4);
        var session = json.RootElement.GetProperty("tasks")[0].GetProperty("sessions")[0];

        session.GetProperty("dateStarted").GetString().Should().Be("2026-09-18T09:00:00+02:00");
        session.GetProperty("durationSeconds").GetDouble().Should().Be(5400);
        session.TryGetProperty("end", out _).Should().BeFalse("the end is no longer stored");
        session.TryGetProperty("duration", out _).Should().BeFalse("the duration string is no longer stored");
    }

    [Test]
    public void Read_WhenGivenAVersionThreeFile_ShouldAssignTaskIdsAndKeepSessionIds()
    {
        var migration = new VersionThreeToFourMigration("unused.json");
        var output = migration.Read(VersionThree(Session("2026-09-18T09:00:00+02:00", "2026-09-18T10:00:00+02:00")));

        var json = Parse(output);
        var task = json.RootElement.GetProperty("tasks")[0];
        var session = task.GetProperty("sessions")[0];

        task.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty, "the task gains a stable id");
        session.GetProperty("id").GetGuid().Should().Be(Guid.Parse(SessionId), "the session keeps its id");
    }

    [Test]
    public void Read_WhenTheSpanHasFractionalSeconds_ShouldRoundToOneDecimal()
    {
        var migration = new VersionThreeToFourMigration("unused.json");
        var output = migration.Read(VersionThree(Session(
            "2026-09-18T09:00:00+02:00",
            "2026-09-18T09:00:00.456+02:00")));

        var json = Parse(output);
        var session = json.RootElement.GetProperty("tasks")[0].GetProperty("sessions")[0];

        session.GetProperty("durationSeconds").GetDouble().Should().Be(0.5);
    }

    private static string Session(string start, string end) =>
        $$"""
        { "id": "{{SessionId}}", "start": "{{start}}", "end": "{{end}}", "duration": "01:00:00", "durationSeconds": 3600 }
        """;

    private static string VersionThree(string sessions) =>
        $$"""
        {
          "version": 3,
          "tasks": [
            { "name": "Report", "bookingElement": "Quarterly", "sessions": [ {{sessions}} ] }
          ]
        }
        """;

    private static JsonDocument Parse(string text) =>
        JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = false });
}
