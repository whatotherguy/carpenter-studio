# UI U6 Catalog Panel

Date: 2026-04-09

## Implemented

- Added `CatalogPanelViewModel` in `src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs`
- Added immutable `CatalogItemViewModel` rows in `src/CabinetDesigner.Presentation/ViewModels/CatalogItemViewModel.cs`
- Wired `CatalogPanelViewModel` into `ShellViewModel` and DI registration
- Replaced the left workspace rail in `src/CabinetDesigner.Presentation/MainWindow.xaml` with a real searchable catalog panel
- Added presentation tests for initial state and filtering behavior

## Behavior

- Search filters cabinet rows in-memory as the user types
- Filter matching checks cabinet type id, display name, category, and default width display
- The panel shows placeholder data clearly labeled as such until a real backend catalog exists
- Each row displays:
  - category
  - cabinet type id
  - display name
  - default width display

## Future hooks

- `CatalogPanelViewModel.ItemActivated` provides a clean attachment point for future add or drag actions
- The item model is immutable, so the panel can be replaced later with backend-backed catalog DTOs without changing the UI shape

## Verification

- Add/update tests live in `tests/CabinetDesigner.Tests/Presentation/CatalogPanelViewModelTests.cs`
- Shell constructor updates live in `tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs`
