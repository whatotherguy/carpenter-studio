# UI U9 Issue Panel

Date: 2026-04-09

## Implemented

- Added `IssuePanelViewModel` in `src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs`
- Added immutable `IssueRowViewModel` in `src/CabinetDesigner.Presentation/ViewModels/IssueRowViewModel.cs`
- Added a small generic `RelayCommand<T>` in `src/CabinetDesigner.Presentation/Commands/RelayCommandT.cs` for row-level actions
- Registered `IssuePanelViewModel` in `PresentationServiceRegistration`
- Wired `ShellViewModel` to own the issue panel and hand it a selection callback
- Added a dedicated validation issues card beneath the canvas in `src/CabinetDesigner.Presentation/MainWindow.xaml`
- Added presentation tests for placeholder initial state, filtering, and selection navigation

## Behavior

- Uses placeholder validation issues when the backend validation summary service is unavailable
- Falls back safely if `IValidationSummaryService` still throws `NotImplementedException`
- Shows severity counts, blocker state, source label, and a scrollable issue list
- Supports in-memory severity filtering even with placeholder data
- Each issue row can select affected entities through the canvas callback when the row carries parseable ids

## Future hooks

- `IssuePanelViewModel.SetSelectionCallback(...)` keeps navigation wiring out of the shell layout
- The panel already maps from `ValidationIssueSummaryDto` when live service data becomes available
- The generic row command can be reused for future row actions without introducing code-behind logic

## Verification

- Add/update tests live in `tests/CabinetDesigner.Tests/Presentation/IssuePanelViewModelTests.cs`
