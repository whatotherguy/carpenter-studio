# UI U5 Status and Commands

Date: 2026-04-08

## Goal

Make the first useful non-canvas shell surface real by exposing the existing shell commands and the live project/status state in the visible window chrome.

## What Changed

- `MainWindow.xaml`
  - Replaced the placeholder shell panels with live command and status surfaces.
  - Kept the existing menu commands visible for keyboard and menu access.
  - Added a clearer top command strip with `New`, `Open`, `Save`, `Close`, `Undo`, and `Redo`.
  - Added a bottom status bar that shows current status text, active project name, project open state, mode, revision, and save state.

- `ShellViewModel.cs`
  - Added display-only properties for shell chrome:
    - `ActiveProjectNameText`
    - `ProjectOpenText`
    - `RevisionText`
    - `SaveStateText`
    - `CurrentStatusText`
  - Kept command state and project state refresh inside the view model so XAML stays simple.
  - Continued to use the existing command objects and project service behavior; no fake shell functionality was introduced.

- `ShellViewModelTests.cs`
  - Added coverage for command enablement driven by the pending project fields.
  - Added coverage for visible project state after `ProjectOpenedEvent`.
  - Added coverage for shell status text refresh after canvas design-change events.

## Notes

- The shell now surfaces real state, but it still delegates actual project persistence and undo/redo behavior to the existing services.
- The canvas remains the center of the workspace; the shell chrome only reports and routes around it.
