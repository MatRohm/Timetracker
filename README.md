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
weekday, Monday first**. Every cell lists that day's bookings — one line per
session (`09:00–10:00 Report (1:00)`) — with a per-day sum (`Σ 1:30`) at the
bottom of the cell. Today's column is highlighted.

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

Below the timer, a table lists every saved entry (task, description, started,
ended, duration).
Click a column header to sort by it; clicking the same header again toggles the
direction. Sortable columns: **Task** (A–Z), **Started**, **Ended** (newest first
on first click) and **Duration** (sorted numerically). Sorting state lives in the
view model (`SortColumn` / `SortAscending`); the grid only shows sort glyphs.

The **Task** and **Description** cells are editable inline: double-click a cell,
type, and press **Enter** to commit (Esc cancels). Changes are persisted to the
JSON file immediately; an empty task name is rejected, the description is optional.

## Data file

Entries are stored at `%USERPROFILE%\timetracker.json`
(e.g. `C:\Users\<you>\timetracker.json`). The file is rewritten atomically after each
stop; every existing entry is always preserved, nothing is ever removed.

```json
[
  {
    "task": "Writing report",
    "description": "Quarterly figures, draft 2",
    "start": "2026-09-19T14:03:21.123+02:00",
    "end": "2026-09-19T15:10:02.456+02:00",
    "duration": "01:06:41",
    "durationSeconds": 4001.3
  }
]
```

`description` is optional — older entries without it load as an empty string.

## Project structure (MVVM)

```
Timetracker/
├── Models/
│   └── TrackerEntry.cs          # One finished time entry
├── Services/
│   └── JsonTrackerRepository.cs # Append-only JSON persistence (atomic writes)
├── ViewModels/
│   ├── TrackerViewModel.cs      # All logic: start/stop, timer, state, sorting
│   ├── EntryRow.cs              # Aggregated display row (grouped sessions)
│   ├── WeekViewModel.cs         # Week view state: 7 day columns, navigation
│   ├── WeekDayViewModel.cs      # One weekday column (header, bookings, total)
│   ├── SuggestionItem.cs        # Autocomplete suggestion (name + total time)
│   ├── ObservableObject.cs      # INotifyPropertyChanged base class
│   ├── RelayCommand.cs          # ICommand implementation
│   ├── IUiTimer.cs              # UI-agnostic timer abstraction
│   └── TrackerStatus.cs         # Info / Success / Error status kinds
├── Views/
│   ├── TrackerForm.cs           # Thin view: builds controls, binds, no logic
│   ├── CommandBindings.cs       # Wires Buttons to ICommands (WinForms)
│   └── FormsUiTimer.cs          # WinForms timer implementation
├── Program.cs                   # Entry point + composition root
└── Timetracker.csproj
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
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

The standalone executable ends up at
`bin\Release\net10.0-windows\win-x64\publish\Timetracker.exe` and runs on any
Windows 10/11 machine without installing .NET.