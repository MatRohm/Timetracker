using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.Plugins.Contracts.ViewModels;

namespace Timetracker.App.Tests.Unit.ViewModels;

/// <summary>
/// Autocomplete suggestions: matches are case-insensitive substrings over the
/// known task names, most used first, capped at eight, and an exact match is never
/// suggested.
/// </summary>
[TestFixture]
public sealed class SuggestionListViewModelTests
{
    [Test]
    public void Refresh_WhenTheQueryIsASubstring_ShouldSuggestMatchesWithTotalsAndExcludeExactMatch()
    {
        var vm = new SuggestionListViewModel();
        var sessions = new[] { Entry("Report", 3600), Entry("Meeting", 1800) };

        vm.Refresh("rep", sessions);

        vm.Suggestions.Should().ContainSingle().Which.Name.Should().Be("Report");
        vm.Suggestions[0].TotalText.Should().Be("01:00:00");
    }

    [Test]
    public void Refresh_WhenAnExactMatchIsTyped_ShouldSuggestNothing()
    {
        var vm = new SuggestionListViewModel();
        var sessions = new[] { Entry("Report", 3600) };

        vm.Refresh("report", sessions);

        vm.Suggestions.Should().BeEmpty("an exact match is not suggested");
    }

    [Test]
    public void Refresh_WhenTheQueryIsEmpty_ShouldClearTheSuggestions()
    {
        var vm = new SuggestionListViewModel();
        var sessions = new[] { Entry("Report", 3600) };
        vm.Refresh("rep", sessions);

        vm.Refresh("   ", sessions);

        vm.Suggestions.Should().BeEmpty("an empty query clears the list");
    }

    [Test]
    public void Refresh_WhenThereAreMoreThanEightMatches_ShouldCapTheList()
    {
        var vm = new SuggestionListViewModel();
        var sessions = Enumerable.Range(0, 12)
            .Select(i => Entry($"Task {i:00}", 1800))
            .ToArray();

        vm.Refresh("Task", sessions);

        vm.Suggestions.Should().HaveCount(8, "at most eight suggestions are shown");
    }

    [Test]
    public void ShowSuggestions_WhenThereAreMatches_ShouldBeTrue()
    {
        var vm = new SuggestionListViewModel();

        vm.Refresh("rep", new[] { Entry("Report", 3600) });

        vm.ShowSuggestions.Should().BeTrue();
    }

    [Test]
    public void ShowSuggestions_WhenThereAreNoMatches_ShouldBeFalse()
    {
        var vm = new SuggestionListViewModel();

        vm.Refresh("nope", new[] { Entry("Report", 3600) });

        vm.ShowSuggestions.Should().BeFalse();
    }

    private static (string Task, double DurationSeconds) Entry(string task, double durationSeconds) =>
        (task, durationSeconds);
}
