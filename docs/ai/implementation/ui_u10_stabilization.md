# UI U10 Stabilization Notes

Date: 2026-04-09
Scope: `CabinetDesigner.Presentation` and `tests/CabinetDesigner.Tests/Presentation`

## What Changed

- Reduced repeated selection-refresh wiring in `ShellViewModel` by routing canvas selection and scene updates through one helper.
- Tightened placeholder labeling in the visible shell chrome so the catalog, property inspector, run summary, and validation issue surfaces now read as placeholder-backed where applicable.
- Cleaned up a few presentation naming and state-reset edges in the run summary and validation issue panels.
- Strengthened presentation tests around shell transitions, selection-driven UI, filtering, empty states, and command refresh notifications.

## Robustness Pass

- `ShellViewModel` now updates the child panels from one selection-sync path instead of repeating the same calls in multiple branches.
- `IssuePanelViewModel` now resets placeholder state through one helper so closed-project and unavailable-service paths stay in sync.
- `RunSummaryPanelViewModel` now uses shared selection prompt text and consistent placeholder labels.
- The run summary placeholder values no longer contain the dash encoding artifact.

## Test Coverage Added

- Shell command notification refresh after project open.
- Shell clearing of selection-driven panels after project close.
- Validation issue panel filter reset and command-state refresh on project close.
- Run summary placeholder labels and state reset behavior.
- Status bar issue-summary reset and whitespace status fallback.

## What is Now Visibly Usable

- The docked shell layout with live menu, toolbar, and status bar.
- Searchable placeholder catalog content.
- A selection-aware property inspector that shows projected cabinet details when a cabinet is selected.
- A run summary panel that reflects selection context without pretending active run data exists yet.
- A validation issues panel with placeholder-backed filtering and entity-navigation wiring.

## What Is Still Placeholder

- Catalog data is still hardcoded presentation data.
- Property editing is still read-only.
- Run summary slot data is still empty because the live run projection is not wired through.
- Validation issues still fall back to placeholder data when the validation service is unavailable.

## Next Prompt Should Tackle

- Wire the catalog and validation panels to real application data sources.
- Add real run summary projection so selection can map into live slot data.
- Replace the remaining read-only property inspector copy with actual editable property flows once the application layer is ready.
