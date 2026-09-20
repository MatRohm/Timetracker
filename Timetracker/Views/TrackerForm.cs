using Timetracker.ViewModels;

namespace Timetracker.Views;

/// <summary>
/// Application shell: hosts the tracker and week tabs, binds the window title and
/// app-level events. All tab content lives in the dedicated tab views; add-in UI
/// comes from the registered hooks.
/// </summary>
public sealed class TrackerForm : Form
{
    private readonly TrackerViewModel _vm;
    private readonly TrackerTabView _trackerView;

    public TrackerForm(TrackerViewModel viewModel, IServiceProvider services)
    {
        _vm = viewModel;

        Text = "Timetracker";
        Font = new Font("Segoe UI", 9.75F);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 560);
        // Fist-smashed-clock app icon in the title bar (and taskbar/alt-tab).
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _trackerView = new TrackerTabView(_vm, services);
        var weekView = new WeekTabView(_vm.Week, services);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(new TabPage("Tracker") { Controls = { _trackerView } });
        tabs.TabPages.Add(new TabPage("Week view") { Controls = { weekView } });

        Controls.Add(tabs);

        // Enter starts when idle and stops while running.
        _trackerView.RunningStateChanged += OnRunningStateChanged;
        DataBindings.Add(nameof(Text), _vm, nameof(TrackerViewModel.Title));

        _vm.ErrorOccurred += OnErrorOccurred;
    }

    private void OnRunningStateChanged() =>
        AcceptButton = _vm.IsRunning ? _trackerView.StopButton : _trackerView.StartButton;

    private void OnErrorOccurred(string message)
    {
        MessageBox.Show(this, message, "Timetracker", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Never lose a running entry: the view model saves it before the window closes.
        _vm.SaveRunningEntryOnClose();
        base.OnFormClosing(e);
    }
}
