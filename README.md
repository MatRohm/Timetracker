# Timetracker

A tiny Windows desktop time tracker: enter a task name, press **Start**, press **Stop** —
the finished entry is appended to a JSON file in your user folder. Entries are never deleted.

## Error handling & logging

Unexpected exceptions never crash the app with a stack-trace dialog. A global
handler catches UI-thread, AppDomain and unobserved-task exceptions, shows a
generic error message, and writes the full details (with timestamp) to
**`Timetracker.log`** in the application's execution directory (next to the
`Timetracker.exe`). If that directory is not writable, the log falls back to
`%TEMP%\Timetracker.log`. The log is append-only. Save/update failures are
logged as well.

## Usage

1. Type a task name.
2. Press **Start** (the task name locks and a live timer runs).
3. Press **Stop** — the entry is saved and the timer resets.

If you close the app while the timer is running, that entry is saved automatically.
Pressing **Enter** in the text field starts the timer; while running, **Enter** stops it.

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

**Pagination**: the grid shows ten tasks per page. When the history spans more
than one page, **◀ Previous / Next ▶** controls with a page indicator appear
below the list. Sorting spans all pages, and the task-name suggestions always
use every saved task — regardless of the visible page.

The **Task** and **Booking element** cells are editable inline: press **F2** on a
selected cell, type, and press **Enter** to commit (Esc cancels). Changes are
persisted to the JSON file immediately; an empty task name is rejected, the
booking element is optional. Started/Ended/Duration stay read-only.

**Double-click a row to start tracking that task immediately**: the timer starts
with the row's task name (a running session is never interrupted; editing stays
on F2).

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

Create **`%USERPROFILE%\timetracker-azdo.json`** (the app shows the expected path
in the panel's status hint when it is missing). An example file lives at
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
is actively used: it watches for idle periods, logoff and shutdown, and writes
one JSON span per period to **`%USERPROFILE%\timetracker-activity.json`**.

- **Active** spans cover periods with user input; they are always logged.
- **Idle** spans are only logged when they last **at least one hour** — shorter
  breaks are ignored entirely.
- Logoff/shutdown closes the open span (also across hard process kills, via a
  small state file), and the next logon starts a new one.

The week view shows the resulting **active/idle time per weekday** in the gray
line below each day's bookings.

### Installation

The week view offers **⏻ Install PC activity monitor** / **⏻ Remove PC activity
monitor** buttons. Installing registers a per-user autostart (HKCU Run key) so
the monitor starts with Windows — no admin rights required; a Windows service
or scheduled task is not necessary for this. The buttons reflect the current
state (exactly one is enabled), and removal takes effect at the next logon.

## Data file

Entries are stored at `%USERPROFILE%\timetracker.json`
(e.g. `C:\Users\<you>\timetracker.json`). The file is rewritten atomically after each
stop; every existing entry is always preserved, nothing is ever removed.

```json
[
  {
    "task": "Writing report",
    "bookingElement": "Quarterly figures",
    "start": "2026-09-19T14:03:21.123+02:00",
    "end": "2026-09-19T15:10:02.456+02:00",
    "duration": "01:06:41",
    "durationSeconds": 4001.3
  }
]
```

`bookingElement` is optional. Older files that stored it under the JSON name
`description` are migrated automatically on load; saves always write the new name.

## Project structure (MVVM)

```
Timetracker/
├── Timetracker/                     # Application project (named after the csproj)
│   ├── Models/
│   │   └── TrackerEntry.cs          # One finished time entry
│   ├── Services/
│   │   ├── JsonTrackerRepository.cs # Append-only JSON persistence (atomic writes)
│   │   └── ErrorLog.cs              # Timestamped error log next to the executable
│   ├── ViewModels/
│   │   ├── TrackerViewModel.cs      # All logic: start/stop, timer, state, sorting
│   │   ├── EntryRow.cs              # Aggregated display row (grouped sessions)
│   │   ├── WeekViewModel.cs         # Week view state: 7 day columns, navigation
│   │   ├── WeekDayViewModel.cs      # One weekday column (header, bookings, total)
│   │   ├── SuggestionItem.cs        # Autocomplete suggestion (name + total time)
│   │   ├── ObservableObject.cs      # INotifyPropertyChanged base class
│   │   ├── RelayCommand.cs          # ICommand implementation
│   │   ├── IUiTimer.cs              # UI-agnostic timer abstraction
│   │   └── TrackerStatus.cs         # Info / Success / Error status kinds
│   ├── Views/
│   │   ├── TrackerForm.cs           # Shell: window, tabs, title binding, close handling
│   │   ├── TrackerTabView.cs        # Tracker tab: input, suggestions, timer, history grid
│   │   ├── WeekTabView.cs           # Week view tab: weekday columns, navigation
│   │   ├── CommandBindings.cs       # Wires Buttons to ICommands (WinForms)
│   │   └── FormsUiTimer.cs          # WinForms timer implementation
│   ├── Program.cs                   # Entry point + composition root
│   └── Timetracker.csproj
├── Timetracker.AzureDevOps/         # Add-in: work item import (issue number → fields)
├── Timetracker.ActivityMonitor/     # Add-in: PC active/idle recording (background exe)
├── Timetracker.slnx                 # XML solution (app + add-ins + tests)
└── Timetracker.Tests/               # NUnit tests with AwesomeAssertions + FakeItEasy
    ├── TrackerViewModelTests.cs
    ├── JsonTrackerRepositoryTests.cs
    ├── WeekViewModelTests.cs
    ├── AzureDevOpsTests.cs
    ├── ActivityMonitorTests.cs
    └── TestDoubles.cs
```

The view model knows nothing about WinForms; the view contains no business logic.
They communicate via data bindings, `ICommand`s, and view-model events.

## Build

Requires the .NET SDK (10.0 or newer):

```pwsh
dotnet build
```

### Single-file desktop build

```pwsh
dotnet publish Timetracker/Timetracker.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

The standalone executable ends up at
`Timetracker\bin\Release\net10.0-windows\win-x64\publish\Timetracker.exe` and runs on any
Windows 10/11 machine without installing .NET.

## App icon

The app icon (a fist smashing a clock) lives at
`Timetracker/Timetracker.ico` as a multi-size ICO (16/32/48/256 px). It is
embedded into the exe via `ApplicationIcon` and shown in the title bar,
taskbar and Alt-Tab view.