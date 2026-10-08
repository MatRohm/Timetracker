# Timetracker

A small cross-platform desktop time tracker (Windows, Linux, macOS) built with
[Avalonia UI](https://avaloniaui.net/). Type a task name, press **Start**,
press **Stop** — the session is saved to a JSON file in your user folder.

## Features

- **Start/stop tracking** with a live timer; **Enter** starts and stops; closing the app saves a running session.
- **Idle auto-stop** after 30 minutes without input; the session ends at the last input.
- **Suggestions** while typing: known task names with their accumulated time.
- **History grid:** one row per task — sorting, column filters, paging, inline editing (**F2**), multi-select delete (**Del**), per-task session editor.
- **Week view:** collapsible day → booking element → task tree with daily totals and copy buttons.
- **Azure DevOps import:** enter a work-item number to fill task name and booking element.
- **PC activity monitor:** optional background process recording active/idle time; the week view shows it per day plus the **untracked time**.
- **Options** tab: file and log locations plus plugin settings.

## Getting started

Requires the .NET SDK 10.0 or newer.

```sh
dotnet build
dotnet test
dotnet run --project Timetracker.App
```

Self-contained single-file executable (use `linux-x64` for Linux):

```sh
dotnet publish Timetracker.App/Timetracker.App.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

CI builds and tests every push on Windows and Linux; the manual **Release** workflow publishes both binaries.

## Configuration and data files

All files live in your user folder (`%USERPROFILE%` / `$HOME`).

| File | Purpose |
|---|---|
| `timetracker.json` | Tracked sessions, grouped per task (versioned format; older files migrate at startup, backed up first). Written atomically. |
| `timetracker-options.json` | Options from the **Options** tab (token stored unencrypted, masked on screen). Written atomically. |
| `timetracker-activity.json` | Active/idle spans from the activity monitor (idle spans ≥ 1 h only). |

Logs: `%LOCALAPPDATA%\Timetracker\logs\` (Windows) or
`$XDG_STATE_HOME/timetracker/logs/` (Linux), daily files, newest 14 kept.
The **Options** tab shows every location with a **Show in explorer** button.

**Azure DevOps:** organization URL, project and a PAT (**Work Items (Read)**
scope) in the **Options** tab; applies without restart; task name becomes
`<id> <title>`, booking element from the **AZE-Element** field. Without a
connection the tracker works normally.

**Activity monitor:** the week view's **⏻ Install PC activity monitor** button
registers a per-user autostart; no admin rights.

## Architecture

Micro-Kernel Architecture: `Timetracker.App` is the kernel (persistence,
session handling, UI shell); feature areas are **plugins** extending it only
through the contract interfaces in `Timetracker.Plugins.Contracts` — the
kernel never references plugin concrete types.

- **Kernel** (`Timetracker.App`): view models, views, JSON persistence, DI wiring; discovers plugins via `GetServices<IUiQuery>`, `GetServices<ITabQuery>`, `GetServices<IAppCommand>`, …
- **Contracts** (`Timetracker.Plugins.Contracts`): the plug-in seam — hook interfaces the kernel calls (`IUiQuery`, `IWeekDayQuery`, `IAppCommand`, …) and host interfaces plugins call into (`ITrackerSessionQuery/Command`, `IOptionQuery`, `IUiTimer`, …). No plugin logic.
- **Plugins** (WeekView, AzureDevOps, ActivityMonitor): implement hooks, self-register in `Timetracker.App/HookRegistry.cs`. Adding a feature = implement a hook + register. MVVM throughout: view models know nothing about Avalonia.

| Project | Role |
|---|---|
| `Timetracker.App` | Micro-kernel (ships as `Timetracker.exe`) |
| `Timetracker.Plugins.Contracts` | Hook + host interfaces, no logic |
| `Timetracker.Plugins.WeekView` / `.AzureDevOps` / `.ActivityMonitor` | Plugins |
| `*.Tests.Unit`, `Timetracker.Tests.UI` | Unit and headless UI tests |
| `Timetracker.Tests.Architecture` | Enforces project references, MVVM layering, naming |

## Contributing

- Track work in [beads](https://github.com/gastownhall/beads) (`bd ready`, `bd create`, `bd close`).
- Commit subjects use one of three types — `feat` (new feature), `fix` (bug
  fix) or `technical` (tests, refactoring, tooling and other purely technical
  work) — optionally with a `(scope)` and a `!` breaking-change marker (see
  [Breaking changes](#breaking-changes)), and end with the issue id
  (`feat: show the week view as a tree (timetracker-66k)`), enforced by the
  `commit-msg` hook — enable once per clone:

  ```sh
  git config core.hooksPath .beads/hooks   # with bd installed
  git config core.hooksPath .githooks      # without bd
  ```

- Test naming: `<Method>_When<Condition>_Should<Result>` (architecture tests enforce it).
- AI agent instructions (Claude Code, opencode) live in `.claude/CLAUDE.md`.

### Breaking changes

A change is **breaking** when something already built, stored or configured
against the previous version stops working. Mark it with `!` after the type or
scope **and** document it in a `BREAKING CHANGE:` footer; the `commit-msg` hook
requires both together, so `!` is never cosmetic:

```
fix!: rename the option key to booking-element (timetracker-9z)

BREAKING CHANGE: existing option files keep the old key; the value is ignored
until re-entered.
```

The three contracts that can break:

| Surface | Breaking | Not breaking |
|---|---|---|
| **Plugin contract** (`Timetracker.Plugins.Contracts`) | a hook/host interface removed or renamed; an interface member added without a default; a member's signature, nullability or threading contract changed; an enum's meaning remapped | a new optional hook or tab; an interface member added *with* a default |
| **On-disk data** (`timetracker.json`, `timetracker-options.json`, `timetracker-activity.json`) | a persisted field renamed or removed so old files lose it without a migration; a stored value's meaning, unit or shape changed; a file moved so existing data is not found | a new field with a default old versions ignore; a migration that reads old files; a version bump still readable by the previous release |
| **Options / integration** (option keys, Azure DevOps mapping, autostart) | an option key renamed so saved values are ignored; a project or field mapping changed so imports land elsewhere; an autostart path changed so the monitor stops launching | a new option or hint; relocalized strings; wording changes |