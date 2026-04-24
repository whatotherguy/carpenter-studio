# Carpenter Studio

Carpenter Studio is a Windows desktop cabinet-design prototype built with .NET 8 and WPF. The current alpha shell includes project startup, rooms, catalog browsing, canvas editing, run summary, property inspection, validation issues, and alpha-limitation guidance.

## Prerequisites

- Windows
- .NET 8 SDK
- Optional: Visual Studio 2022 if you want to run/debug from the IDE

## Repo Layout

- `src/CabinetDesigner.App`: WPF application entrypoint
- `src/CabinetDesigner.Presentation`: shell, views, viewmodels, and commands
- `src/CabinetDesigner.Application`: application services, DTOs, diagnostics, and orchestration
- `src/CabinetDesigner.Persistence`: SQLite persistence and startup migrations
- `tests/`: automated test projects

## Build

From the repo root:

```powershell
dotnet build CabinetDesigner.sln -warnaserror
```

## Run The App

The actual desktop app launches from `CabinetDesigner.App`:

```powershell
dotnet run --project src/CabinetDesigner.App/CabinetDesigner.App.csproj
```

If you prefer Visual Studio, open `CabinetDesigner.sln` and set `CabinetDesigner.App` as the startup project.

## Test

Run the full automated suite from the repo root:

```powershell
dotnet test CabinetDesigner.sln
```

## Local App Data

The app stores local state under:

- Database: `%LOCALAPPDATA%\CarpenterStudio\carpenter-studio.db`
- Logs: `%LOCALAPPDATA%\CarpenterStudio\logs`

If startup fails or a manual test hits an unexpected error, check the newest `app-YYYYMMDD.log` file in the logs folder.

## Manual Testing Notes

The startup screen should let you either create a new project or reopen a recent one. Once a project is open, the main shell exposes rooms, catalog, canvas, property inspector, run summary, and issues.

Useful shortcuts during manual testing:

- `Ctrl+N`: new project
- `Ctrl+O`: open project
- `Ctrl+S`: save
- `Ctrl+Z`: undo
- `Ctrl+Y`: redo
- `Ctrl+Shift+Z`: redo
- `Delete`: delete selected cabinet
- `F1`: open alpha limitations

Detailed session checklist:

- [docs/ai/alpha_prep/manual_polish_checklist.md](/C:/Users/whato/repos/carpenter-studio/docs/ai/alpha_prep/manual_polish_checklist.md)

## Suggested Manual Smoke Flow

1. Launch the app with `dotnet run --project src/CabinetDesigner.App/CabinetDesigner.App.csproj`.
2. Create a new project from the startup screen.
3. Add a room and confirm `Enter` in the room name box works.
4. Add a cabinet from the catalog and verify the property inspector and run summary update.
5. Try the global shortcuts once each.
6. Press `F1` and confirm the Alpha Limitations dialog opens.
7. Save the project and reopen it.

## Current Alpha Scope

This build intentionally surfaces some incomplete workflows instead of failing silently. When something is not yet supported in alpha, use `F1` in the app to review the current alpha limitations list.
