using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using NUnit.Framework;
using Timetracker.App.Interfaces;
using Timetracker.App.Models;
using Timetracker.App.Tests.Unit;
using Timetracker.App.ViewModels;
using Timetracker.App.Views;

namespace Timetracker.Tests.UI;

/// <summary>
/// Inline edits in the history grid must reach the repository. The grid routes a
/// committed cell edit to the view model by the edited column's property name, so
/// every editable column has to be identifiable that way; a column the router does
/// not recognise stages its text in the row but never saves it.
/// </summary>
public sealed class InlineEditPersistTests
{
    [AvaloniaTest]
    public void TrackerTabView_WhenBookingElementEditedInline_ShouldPersistTheNewValue()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", "old"));

        CommitInlineEdit(repo, grid => grid.Columns[3], row => row.BookingElement = "new");

        RepositoryFake.Persisted(repo).Should().ContainSingle()
            .Which.BookingElement.Should().Be("new");
    }

    [AvaloniaTest]
    public void TrackerTabView_WhenTaskEditedInline_ShouldPersistTheNewValue()
    {
        var (repo, _) = RepositoryFake.Create(Entry("Report", "old"));

        CommitInlineEdit(repo, grid => grid.Columns[2], row => row.Task = "Edited");

        RepositoryFake.Persisted(repo).Should().ContainSingle()
            .Which.Task.Should().Be("Edited");
    }

    /// <summary>
    /// Realizes the tracker view on a headless grid, stages <paramref name="stage"/>
    /// in the row, then begins and commits the cell edit through the real DataGrid so
    /// <c>CellEditEnded</c> fires exactly as it does for a user editing the cell. The
    /// cell edit is committed after <paramref name="stage"/> so the router sees the
    /// staged value.
    /// </summary>
    private static void CommitInlineEdit(
        ITrackerRepository repo,
        Func<DataGrid, DataGridColumn> column,
        Action<EntryRow> stage)
    {
        var viewModel = TrackerViewModelFactory.Create(repo);
        var view = new TrackerTabView(viewModel, []);
        var window = new Window { Content = view, Width = 900, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
        var row = viewModel.Entries[0];
        grid.SelectedItem = row;
        grid.CurrentColumn = column(grid);
        Dispatcher.UIThread.RunJobs();

        grid.BeginEdit();
        Dispatcher.UIThread.RunJobs();
        stage(row);
        grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        window.Close();
    }

    private static TrackerEntry Entry(string task, string bookingElement) => new()
    {
        Task = task,
        BookingElement = bookingElement,
        Start = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(2)),
        End = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(2)),
        Duration = "01:00:00",
        DurationSeconds = 3600,
    };
}
