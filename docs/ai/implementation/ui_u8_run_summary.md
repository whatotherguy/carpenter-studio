# UI U8 Run Summary

Date: 2026-04-09

## Implemented

- Added `RunSummaryPanelViewModel` in `src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs`
- Added immutable `RunSlotViewModel` in `src/CabinetDesigner.Presentation/ViewModels/RunSlotViewModel.cs`
- Registered `RunSummaryPanelViewModel` in `PresentationServiceRegistration`
- Wired `ShellViewModel` to forward canvas selection changes into the run summary panel
- Reworked the left rail in `src/CabinetDesigner.Presentation/MainWindow.xaml` to include a real run summary card beneath the catalog
- Added presentation tests for initial state, selection updates, and project-close reset behavior

## Behavior

- The panel is visible now and does not depend on the backend run summary projection being implemented
- Selection changes update the panel's selection copy so it remains useful in the UI today
- The slot list is ready for future data, but stays empty until the application layer can supply real run data
- Placeholder text is clearly marked and avoids pretending the backend summary exists

## Future hooks

- `RunSummaryPanelViewModel.Slots` already binds to a slot template, so real run data can render without changing the shell layout
- `RunSummaryPanelViewModel.OnSelectionChanged(...)` is the integration point for future canvas-to-run coordination

## Verification

- Add/update tests live in `tests/CabinetDesigner.Tests/Presentation/RunSummaryPanelViewModelTests.cs`
