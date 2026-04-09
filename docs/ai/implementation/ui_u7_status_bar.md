# UI U7 Status Bar

Date: 2026-04-09

## Implemented

- Added `StatusBarViewModel` in `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs`
- Registered `StatusBarViewModel` in `PresentationServiceRegistration`
- Wired `ShellViewModel` to forward active project and canvas status message into the status bar
- Reworked the bottom strip in `src/CabinetDesigner.Presentation/MainWindow.xaml` into a real status bar
- Added presentation tests for status bar defaults and event-driven updates

## Behavior

- Displays validation counts in a compact issue summary
- Shows the current revision label and save state
- Surfaces the latest canvas status message
- Uses placeholder-safe validation refresh logic so the UI does not break while `IValidationSummaryService` remains stubbed

## Future hooks

- The status bar already subscribes to project and design lifecycle events
- Validation counts will automatically start reflecting real data once `IValidationSummaryService` is implemented

## Verification

- Add/update tests live in `tests/CabinetDesigner.Tests/Presentation/StatusBarViewModelTests.cs`
