# Timetracker

A tiny cross-platform desktop time tracker (Linux, Windows, macOS): enter a task
name, press **Start**, press **Stop** — the finished entry is appended to a JSON
file in your user folder. Entries are never deleted.

The UI is built with [Avalonia UI](https://avaloniaui.net/), so the same binary
runs on Linux and Windows. The only platform-specific pieces (idle detection and
autostart installation) are selected at runtime.

## Error handling & logging

Unexpected exceptions never crash the app with a stack-trace dialog. A global
handler catches AppDomain and unobserved-task exceptions, shows a generic error
message, and writes the full details (with timestamp) to **`Timetracker.log`** in
the application's execution directory (next to the executable). If that directory
is not writable, the log falls back to the temp folder. The log is append-only.
Save/update failures are logged as well.

## Usage

1. Type a task name.
2. Press **Start** (the task name locks and a live timer runs).
3. Press **Stop** — the entry is saved and the timer resets.

If you close the app while the timer is running, that entry is saved automatically.
Pressing **Enter** in the text field starts the timer; while running, **Enter** stops it.

### Idle auto-stop

If there is no keyboard or mouse input for **30 minutes**, a running timer stops
by itself. The entry ends at the moment of the last input, so the idle stretch is
not billed to the task, and the status line reports what happened
(e.g. `⏸ Stopped after 30 min idle – saved 00:15:00`). Starting again afterwards
works normally.

Idle detection is platform-specific: Windows uses the Win32 last-input timestamp;
Linux uses the X11 `XScreenSaver` extension. Where neither is available the app
never auto-stops, but keeps tracking as before.

Inline edits of **Task** apply to every session of that task, which also merges
(or splits) its group in the list.

## Week view

The second tab shows the selected week as a **grid with seven columns, one per
weekday, Monday first**. Every cell lists that day's bookings — **one line per
booking element with its total duration** (`Project X (1:30)`), entries without
a booking element fall back to their task names — with a per-day sum (`Σ 1:30`)
at the bottom of the cell. Today's column is highlighted.

Below each day's bookings, a gray **PC activity line** shows the recorded
machine activity for that day (`PC 7:15 active · 1:20 idle`), based on the
activity monitor's log (see below).

The **Group by booking element** checkbox switches the grouping: checked
(default) groups by booking element; unchecked shows one line per task name
instead.

While grouping by booking element, each booking element line has a small **copy
button** (⧉) that copies the task names of that line's tracking entries for the
day, one per line.

Navigate with **◀ Previous week** / **Next week ▶**; **● Current week** jumps
back to today's week. The title shows the ISO week number and date range.
The view starts on the current week, stays on the selected week while you
track time, and updates live as sessions are added or edited. Durations
display as `h:mm`.

## Suggestions & task totals

While you type a task name, a suggestion list appears below the field. It shows
known task names (substring match, case-insensitive, most-used first) together
with their **accumulated time**. Pick one with a **double-click** (or **↓** then
**Enter**); **Esc** hides the list. An exact match is never suggested — just
keep typing or press Start.

The history list groups all sessions with the same task name (case-insensitive)
into a single row: **Started** is the first start, **Ended** the last end, and
**Duration** is the total of every session. So tracking a task again simply adds
its time to that task's row. The JSON file itself remains an append-only session
log — each stop still appends one record; grouping only happens in the view.

## History list

Below the timer, a table lists every saved entry (task, booking element, started,
ended, duration).
Click a column header to sort by it; clicking the same header again toggles the
direction. Sortable columns: **Task** (A–Z), **Started**, **Ended** (newest first
on first click) and **Duration** (sorted numerically). Sorting state lives in the
view model (`SortColumn` / `SortAscending`); the grid only shows sort glyphs.
Drag the separator between two headers to resize the columns; the two row-action
columns keep their fixed width.

**Pagination**: the grid shows ten tasks per page. When the history spans more
than one page, **◀ Previous / Next ▶** controls with a page indicator appear
below the list. Sorting spans all pages, and the task-name suggestions always
use every saved task — regardless of the visible page.

The **Task** and **Booking element** cells are editable inline: press **F2** on a
selected cell, type, and press **Enter** to commit (Esc cancels). Changes are
persisted to the JSON file immediately; an empty task name is rejected, the
booking element is optional. Started/Ended/Duration stay read-only.

**Use the play button on a row to start tracking that task immediately**: the
timer starts with the row's task name (a running session is never interrupted; the
button is disabled while one runs). Editing stays on F2.

**Press Del to delete the selected entries**: a confirmation dialog shows what
will be removed (task names / session counts). Deletion removes all sessions of
the selected tasks from the JSON file and cannot be undone.

**Multi-selection for deleting**: click selects a row, **SHIFT+CLICK** selects
the whole range from the previously selected row, and **CTRL+CLICK** adds or
removes single rows from the selection. Pressing **Del** removes every selected
row together.

## Azure DevOps integration

The tracker can fetch task names from Azure DevOps work items. The integration
lives in its own project (`Timetracker.AzureDevOps`) and adds an
**Azure DevOps issue** band to the tracker tab: enter an issue number and press
**⇩ Apply** (or **Enter**).

The task name is filled as `<issue number> <title>` and the **booking element**
is taken from the work item's custom field **AZE-Element** (`Custom.AZEElement`),
which is used for the next started session on that task.

### Configuration

Create **`timetracker-azdo.json`** in your user folder
(`%USERPROFILE%` on Windows, `$HOME` on Linux). An example file lives at
`Timetracker.AzureDevOps/timetracker-azdo.example.json`:

```json
{
  "url": "https://dev.azure.com/your-organization",
  "project": "YourProject",
  "pat": "your-personal-access-token"
}
```

- `url` — organization/collection URL
- `project` — project the work items live in
- `pat` — personal access token (sent as Basic password; only "Read work items"
  permission is needed)

If the file is missing, broken, or incomplete, the panel shows a hint and apply
attempts fail with a clear message; the tracker works normally without it.

## PC activity monitor

The separate project **`Timetracker.ActivityMonitor`** records when the computer
is actively used: it watches for idle periods and writes one JSON span per period
to **`timetracker-activity.json`** in your user folder (`%USERPROFILE%` on
Windows, `$HOME` on Linux).

- **Active** spans cover periods with user input; they are always logged.
- **Idle** spans are only logged when they last **at least one hour** — shorter
  breaks are ignored entirely.
- Session end (logoff/shutdown) closes the open span (also across hard process
  kills, via a small state file), and the next start begins a new one.

Idle detection is platform-specific: Windows uses the Win32 last-input timestamp
(`GetLastInputInfo`); Linux uses the X11 `XScreenSaver` extension. On a platform
where neither is available the monitor still records active time, it just never
detects idle.

The monitor keeps a plain-text log **`Timetracker.ActivityMonitor.log` next to its
executable**: one timestamped entry per event, tagged with the method that wrote it
(startup, a polling heartbeat every five minutes, state-file handling, and
shutdown).

The week view shows the resulting **active/idle time per weekday** in the gray
line below each day's bookings.

### Installation

The week view offers **⏻ Install PC activity monitor** / **⏻ Remove PC activity
monitor** buttons. Installing registers a per-user autostart so the monitor starts
at logon — no admin rights required. The buttons reflect the current state
(exactly one is enabled), and removal takes effect at the next logon.

- **Windows** writes the HKCU Run key (`TimetrackerActivityMonitor`).
- **Linux** writes a freedesktop autostart entry at
  `~/.config/autostart/timetracker-activity-monitor.desktop`.

The publish step copies the monitor executable next to the app automatically, so
the install button works out of the box.

## Data file

Entries are stored at `timetracker.json` in your user folder
(`C:\Users\<you>\timetracker.json` on Windows, `~/.local/share` style `$HOME` on
Linux). The file is rewritten atomically after each stop; every existing entry is
always preserved, nothing is ever removed.

The file carries a `version` marker and groups everything worked on one task into a
single record, so it reads like a list of tasks rather than a flat log:

```json
{
  "version": 2,
  "tasks": [
    {
      "name": "Writing report",
      "bookingElement": "Quarterly figures",
      "sessions": [
        {
          "start": "2026-09-19T14:03:21.123+02:00",
          "end": "2026-09-19T15:10:02.456+02:00",
          "duration": "01:06:41",
          "durationSeconds": 4001.3
        }
      ]
    }
  ]
}
```

`bookingElement` is optional. A task's booking element is the first non-empty one
among its sessions, or empty when none carries one. Sessions with the same task name
are grouped case-insensitively, keeping the first spelling that was seen. Saves
always write this format.

Files without a version marker are the older (version&nbsp;1) flat array. They are
read transparently, and at startup the app rewrites them to version&nbsp;2 in place:
the sessions are grouped by task name as described above. Before rewriting, the
original file is copied to `timetracker.json.v1-backup` so the migration can be
undone by hand. Version&nbsp;1 entries that stored the booking element under the JSON
name `description` are migrated as well. An unrecognised, newer version is left
untouched rather than overwritten.

## Project structure (MVVM + add-in hooks)

The solution follows a small hook system: `Timetracker.Plugins` defines the
interfaces (`IUiContributor` for tab UI bands, `IWeekDayContributor` for per-day
summary lines, `IAppHook` for startup/close, plus host interfaces the add-ins
call). Every add-in registers its services in **one** place —
`Timetracker/HookRegistry.cs` — which builds a
`Microsoft.Extensions.DependencyInjection` container. The shell and tab views
resolve only the hook interfaces, never concrete add-in types; add-ins never
reference the main app project (they get hosts via `UiHostAccessor`).

```
Timetracker/
├── Timetracker.App/                 # Application project (named after the csproj)
│   ├── HookRegistry.cs              # DI container: every component registers here
│   ├── Interfaces/                  # Project contracts (ITrackerRepository, IUiTimer, ...)
│   ├── Models/
│   │   └── TrackerEntry.cs          # One finished time entry
│   ├── Services/
│   │   ├── JsonTrackerRepository.cs # Versioned JSON persistence (atomic writes)
│   │   ├── TrackerFileFormat.cs     # Maps sessions to/from the one-record-per-task shape
│   │   ├── TrackerFileMigrator.cs   # Chains file upgrades, one step per version
│   │   ├── TrackerFileMigrations.cs # The v1→v2 step (from/to version constants)
│   │   ├── VersionOneFileFormat.cs  # Reads the old unversioned session array
│   │   ├── AvaloniaUiTimer.cs       # IUiTimer implementation (ticks on the UI thread)
│   │   └── ErrorLog.cs              # Timestamped error log next to the executable
│   ├── ViewModels/
│   │   ├── TrackerViewModel.cs      # All logic: start/stop, timer, state, sorting
│   │   ├── EntryRow.cs              # Aggregated display row (grouped sessions)
│   │   ├── WeekViewModel.cs         # Week view state: 7 day columns, navigation
│   │   ├── WeekDayViewModel.cs      # One weekday column (header, bookings, total)
│   │   ├── SuggestionItem.cs        # Autocomplete suggestion (name + total time)
│   │   ├── ObservableObject.cs      # INotifyPropertyChanged base class
│   │   ├── RelayCommand.cs          # ICommand implementation
│   │   └── TrackerStatus.cs         # Info / Success / Error status kinds
│   ├── Views/
│   │   ├── TrackerWindow.cs         # Shell: window, tabs, title binding, close handling
│   │   ├── TrackerTabView.cs        # Tracker tab: composes input, toolbar, grid, pager
│   │   ├── WeekTabView.cs           # Week tab: composes header, day columns, status line
│   │   ├── Components/              # Reusable view pieces (Timetracker.Views.Components)
│   │   │   ├── TaskInputField.cs    # Task-name input with autocomplete
│   │   │   ├── EntryHistoryGrid.cs  # History grid: row actions, sorting, delete, inline edit
│   │   │   ├── WeekDaysGrid.cs      # Seven weekday columns with per-line copy
│   │   │   ├── DeleteConfirmation.cs# Shared delete confirmation dialog
│   │   │   └── ViewBrushes.cs       # Shared view colors
│   ├── App.cs                       # Composition root: container, hooks, error handling
│   ├── Program.cs                   # Entry point (Avalonia bootstrap)
│   └── Timetracker.App.csproj
├── Timetracker.Plugins/             # Hook interfaces + UI host accessor (no logic)
│   └── Interfaces/                  # IUiContributor, IAppHook, ITrackerUiHost, ...
├── Timetracker.AzureDevOps/         # Add-in: work item import (issue number → fields)
├── Timetracker.ActivityMonitor/     # Add-in: PC active/idle recording (background exe)
│   └── Interfaces/                  # IActivityMonitorInstaller, IIdleTimeProvider
├── Timetracker.slnx                 # XML solution (app + add-ins + tests)
├── Timetracker.App.Tests.Unit/      # App unit tests (view models, services)
├── Timetracker.Plugins.Tests.Unit/  # Plugins unit tests (UI host accessor)
├── Timetracker.AzureDevOps.Tests.Unit/  # Azure DevOps unit tests (config, client, service)
├── Timetracker.ActivityMonitor.Tests.Unit/  # Activity monitor unit tests
├── Timetracker.Tests.UI/            # UI tests: app views + add-in panels (headless Avalonia)
└── Timetracker.Tests.Architecture/  # Architecture rules (references, test naming)
```

Every project that defines interfaces keeps them in its `Interfaces/` folder
(`Timetracker.Interfaces`, `Timetracker.Plugins.Interfaces`,
`Timetracker.ActivityMonitor.Interfaces`), so a project's contracts are found in
one place. An architecture rule enforces this.

The app project keeps the assembly name `Timetracker`, so the shipped executable
is still `Timetracker` (`Timetracker.exe` on Windows). Only the project folder and
file changed. C# namespaces remain `Timetracker.*` for the same reason.

Test projects follow the `<ProjectName>.Tests.<TestType>` convention and are
kept next to the project they cover (one test project per source project where
tests are needed). Unit tests use NUnit with AwesomeAssertions + FakeItEasy;
UI tests use the Avalonia headless NUnit platform. Each UI test assembly needs
at least one test fixture marked with an explicit `[TestFixture]`, otherwise the
NUnit adapter does not discover the headless tests.

**Test method names** follow
`<MethodTested>_When<StateCondition>_Should<ExpectedResult>`. For UI tests the
first part is the tested component, e.g.
`TrackerTabView_WhenRendered_ShouldShowHistoryRowsAndStartButton`; for unit
tests it is the tested member, e.g.
`Poll_WhenIdleLastsAtLeastOneHour_ShouldLogAnIdleSpan`. The only underscores are
the `_When` and `_Should` separators; each part is PascalCase. Unit-test classes
are named `<ClassTested>Tests` and carry `[TestFixture]`.

**`Timetracker.Tests.Architecture`** enforces the structural rules and runs with
the rest of the suite:

- A product project must not reference `Timetracker.App`.
- A product project other than the app may only reference `Timetracker.Plugins`.
- UI-test methods must follow the naming pattern above.
- A unit-test class must carry `[TestFixture]` and be named `<ClassTested>Tests`
  after a production class or interface.
- A unit-test method must be named after a real member of that production type.
- Every production interface must live in its project's `.Interfaces` namespace.

Test projects (`*.Tests.Unit`, `*.Tests.UI`) are exempt from the reference rules:
they may reference whatever they verify, so the merged UI project can reference
both `Timetracker.App` and `Timetracker.AzureDevOps`.

It checks project references against the `.csproj` files and test names through
ArchUnitNET, so the other projects are built but not referenced (their assemblies
are copied next to the architecture test host for analysis).

The view model knows nothing about Avalonia; the view contains no business logic.
They communicate via data bindings, `ICommand`s, and view-model events. The two tab
views are composed from small, focused controls in `Timetracker.Views.Components`
(task input, history grid, week days grid), each owning one part of the screen.

## Build

Requires the .NET SDK (10.0 or newer). The UI targets plain `net10.0`, so it
builds and runs on Linux, Windows and macOS:

```sh
dotnet build
dotnet test
```

### Build process cleanup

`Directory.Build.rsp` sets `/nodeReuse:false` for all command-line builds. MSBuild
otherwise leaves its worker processes running for 15 minutes; repeated builds stack
them up until the machine runs out of memory (no swap on the current dev box, so it
freezes outright). The file is read automatically by `dotnet build`, `test`,
`format` and `publish` — pass `-noAutoResponse` to bypass it for one command.

### Git hooks

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/)
and are enforced by the shared `commit-msg` hook. Enable it once per clone:

```sh
git config core.hooksPath .githooks
```

### Self-contained publish

Windows (single-file, portable):

```pwsh
dotnet publish Timetracker.App/Timetracker.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

Linux:

```sh
dotnet publish Timetracker.App/Timetracker.App.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

The standalone executable ends up under `Timetracker.App/bin/Release/net10.0/<rid>/publish/`
and runs without installing .NET.

## App icon

The app icon (a fist smashing a clock) lives at `Timetracker.App/Timetracker.png`
and `Timetracker.ico`. The PNG is embedded as the window icon (title bar); the ICO
is set as `<ApplicationIcon>`, which Windows uses for the taskbar, Alt-Tab and
Explorer. The Azure DevOps favicon is likewise embedded as a PNG
(`Timetracker.AzureDevOps/azure-favicon.png`).