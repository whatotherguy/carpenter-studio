# UI U7 Property Inspector

Date: 2026-04-09

## What Changed

Implemented a first-pass right-side Property Inspector that reacts to canvas selection through the existing Presentation shell wiring.

### Files Updated

- `src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/PropertyRowViewModel.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `tests/CabinetDesigner.Tests/Presentation/PropertyInspectorViewModelTests.cs`
- `tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs`

## Behavior

- When nothing is selected, the inspector shows a clear empty state.
- When exactly one cabinet is selected, the inspector shows:
  - cabinet ID
  - cabinet label
  - cabinet type display name
  - render state
  - read-only/editability status
  - data source
- When multiple cabinets are selected, the inspector falls back to a read-only selection summary instead of pretending inline editing exists.
- Selection changes are forwarded from `ShellViewModel` using the existing `EditorCanvasViewModel.SelectedCabinetIds` and `EditorCanvasViewModel.Scene` properties.

## Data Source

The inspector uses the projected canvas scene from `ISceneProjector` / `RenderSceneDto` and does not bypass into the domain layer.

That is the only currently available presentation-safe selected-entity data source in this codebase.

## Blockers For True Editing

Property editing is still blocked by missing application-layer support:

- `IRunService.GetRunSummary(...)` throws `NotImplementedException`
- `IRunService.SetCabinetOverrideAsync(...)` throws `NotImplementedException`
- there is no service for resolving selected cabinet details into a structured property model
- `IValidationSummaryService` is not yet usable for live inspector validation rows

Because of that, the inspector is intentionally read-only for now.

## Notes

- The inspector is structured so editable rows can be added later without changing the selection flow.
- Tests cover initial empty state, selection-to-details transitions, clearing selection, and shell wiring.
