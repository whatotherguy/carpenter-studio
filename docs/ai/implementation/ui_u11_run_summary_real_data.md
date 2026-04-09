# UI U11 Run Summary Real Data

Date: 2026-04-09
Scope: `CabinetDesigner.Application`, `CabinetDesigner.Presentation`, and `tests/CabinetDesigner.Tests/Presentation`

## What Changed

- Wired the run summary panel to a presentation-safe application adapter instead of placeholder-only state.
- Kept the panel centered on active run data while preserving selection-driven slot highlighting in the shell.
- Preserved explicit empty-state handling for both "no project open" and "project open, but no runs" cases.
- Updated presentation tests to cover project open and close transitions, selection updates, active run refresh, and empty-state behavior.

## Implementation Notes

- `RunSummaryService` now projects the current persisted working state into a small run summary read model.
- `RunSummaryPanelViewModel` asks that adapter for the active summary on project and design events, then merges in current selection context for slot highlighting.
- When no project is open, the panel now clears selection-facing state and keeps the placeholder copy honest.
- The shell still forwards canvas selection changes into the run summary panel, so selection-driven updates continue to work.

## What Is Now Visibly Usable

- A run summary panel that shows the active run, total width, cabinet count, and slot list from real application state.
- Slot highlighting that reacts to canvas selection and keeps the selected cabinet context visible.
- Honest empty states for no project, no runs, and empty selection cases.

## What Is Still Placeholder

- The run summary still uses a read-only presentation projection and does not support direct editing or canvas placement actions.
- There is no reverse navigation from run slots back into the editor canvas yet.
- If the application state has no runs, the panel still falls back to explicit empty-state copy.

## Next Prompt Should Tackle

- Connect the run summary panel to slot activation or canvas focus if that interaction becomes available.
- Replace the remaining read-only run summary projection with a richer project-backed source only if the application layer grows one.
- Add any follow-up shell affordances needed for bidirectional selection without changing the current layout.
