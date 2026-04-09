# UI U9 Shell Refactor

Date: 2026-04-09

## Goal
Break the MVP shell window into maintainable XAML pieces without changing behavior.

## What Changed
- Extracted the toolbar into `ShellToolbarView`.
- Extracted the catalog panel into `CatalogPanelView`.
- Extracted the property inspector into `PropertyInspectorView`.
- Extracted the run summary panel into `RunSummaryPanelView`.
- Extracted the validation issues panel into `IssuePanelView`.
- Extracted the bottom status strip into `StatusBarView`.
- Added `ShellPanelHeaderView` so the repeated panel title/subtitle/badge chrome lives in one reusable control.
- Moved the shared shell brushes, styles, and data templates into `Views/Resources/ShellResources.xaml`.
- Simplified `MainWindow.xaml` so it now composes reusable views instead of hosting large inline panel trees.

## Binding Shape
- The toolbar and workspace header still bind to `ShellViewModel`.
- Each extracted panel binds to its child view model through `DataContext` at the usage site.
- The shared item templates continue to bind to the existing row/item view-models.

## Behavior Notes
- No business logic was moved into code-behind.
- The canvas/workspace region remains inline so the existing editor hosting path stays unchanged.
- All visible text, commands, and list layouts were kept equivalent to the MVP shell.

## Result
The shell window is now easier to extend because each major region has a dedicated XAML surface, while `MainWindow.xaml` keeps only composition and top-level layout.

## Build Triage
- I verified that `CabinetDesigner.Application` and `CabinetDesigner.Editor` build cleanly.
- `CabinetDesigner.Rendering` still fails during MSBuild restore/project-reference negotiation in this environment, so the presentation build could not be fully confirmed here.
