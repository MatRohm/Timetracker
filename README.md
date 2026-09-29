# Timetracker

A small cross-platform desktop time tracker (Windows, Linux, macOS) built with
[Avalonia UI](https://avaloniaui.net/). Type a task name, press **Start**, press
**Stop** — the session is saved to a JSON file in your user folder.

## Features

- **Start/stop tracking** with a live timer; **Enter** starts and stops. Closing
  the app saves a running session.
- **Idle auto-stop:** after 30 minutes without keyboard or mouse input the timer
  stops, and the session ends at the last input.
- **Suggestions** while typing: known task names with their accumulated time.
- **History grid:** one row per task (all its sessions summed), with sorting,
  per-column filters, paging, inline editing (**F2**), multi-select delete
  (**Del**) and a per-task editor for individual sessions.
- **Week view:** a collapsible day → booking element → task tree with daily
  totals and copy buttons for task names.
- **Azure DevOps import:** enter a work-item number to fill the task name and
  booking element (see [Configuration](#configuration-and-data-files)).
- **PC activity monitor:** an optional background process that records active and
  idle time; the week view shows it per day, together with the **untracked time**
  (PC active time no session covers, highlighted from 15 minutes).

## Getting started

Requires the .NET SDK 10.0 or newer.

```sh
dotnet build
dotnet test
dotnet run --project Timetracker.App
```

Self-contained single-file executable (runs without installing .NET); use
`linux-x64` for Linux:

```sh
dotnet publish Timetracker.App/Timetracker.App.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

The output lands in `Timetracker.App/bin/Release/net10.0/<rid>/publish/`,
together with the activity monitor executable. CI builds and tests every push on
Windows and Linux; the manual **Release** workflow publishes both binaries.

## Configuration and data files

All files live in your user folder (`%USERPROFILE%` on Windows, `$HOME` on Linux)
unless noted otherwise.

| File | Purpose |
|---|---|
| `timetracker.json` | Tracked sessions, grouped per task (versioned format; older files are migrated at startup and backed up as `timetracker.json.v1-backup`). Written atomically. |
| `timetracker-azdo.json` | Azure DevOps connection (optional, see below). |
| `timetracker-activity.json` | Active/idle spans from the activity monitor. Idle spans are recorded only when they last at least one hour. |
| `Timetracker.log` | Error log, next to the executable (falls back to the temp folder). |

**Azure DevOps:** create `timetracker-azdo.json` (template:
`Timetracker.Plugins.AzureDevOps/timetracker-azdo.example.json`):

```json
{
  "url": "https://dev.azure.com/your-organization",
  "project": "YourProject",
  "pat": "your-personal-access-token"
}
```

The personal access token only needs the **Work Items (Read)** scope. The task
name becomes `<id> <title>`, and the booking element comes from the work item's
**AZE-Element** field. Without the file the tracker works normally.

**Activity monitor:** the week view's **⏻ Install PC activity monitor** button
registers a per-user autostart (HKCU Run key on Windows,
`~/.config/autostart/` on Linux); no admin rights are needed.

## Architecture

The app is a shell with plugins. `Timetracker.Plugins.Contracts` defines the hook
interfaces (`IUiContributor`, `IWeekDayContributor`, `IAppHook`) and the host
interfaces plugins call back into. Every component registers in
`Timetracker.App/HookRegistry.cs`; the shell resolves only the interfaces. The UI
follows MVVM: view models know nothing about Avalonia, views hold no logic.

| Project | Role |
|---|---|
| `Timetracker.App` | Shell, view models, views, JSON persistence (ships as `Timetracker.exe`) |
| `Timetracker.Plugins.Contracts` | Hook and host interfaces, no logic |
| `Timetracker.Plugins.AzureDevOps` | Work-item import |
| `Timetracker.Plugins.ActivityMonitor` | Idle detection and the background monitor executable |
| `*.Tests.Unit` | Unit tests per project (NUnit, AwesomeAssertions, FakeItEasy) |
| `Timetracker.Tests.UI` | Headless Avalonia UI tests |
| `Timetracker.Tests.Architecture` | Enforces project references, MVVM layering, interface placement and test naming |

## Contributing

- Track work in [beads](https://github.com/gastownhall/beads) (`bd ready`,
  `bd create`, `bd close`).
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/)
  and end with the issue id, e.g. `feat: show the week view as a tree (timetracker-66k)`.
  A `commit-msg` hook enforces this; enable it once per clone:

  ```sh
  git config core.hooksPath .beads/hooks   # with bd installed (also runs its hooks)
  git config core.hooksPath .githooks      # without bd
  ```

- Test methods are named `<Method>_When<Condition>_Should<Result>`; the
  architecture tests report any naming or structure violation.
- Instructions for AI agents (Claude Code and opencode) live in `.claude/CLAUDE.md`.
