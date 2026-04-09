# UI U10 Property Inspector

Implemented the first presentation-backed property inspector for Carpenter Studio as a visible right-rail panel beside the canvas.

## What Changed

- Added `PropertyInspectorViewModel` and `PropertyRowViewModel` in `src/CabinetDesigner.Presentation/ViewModels/`.
- Wired the inspector into `ShellViewModel` so canvas selection changes flow into the panel.
- Registered the inspector in `PresentationServiceRegistration`.
- Updated `MainWindow.xaml` so the workspace now shows:
  - canvas on the left
  - property inspector on the right
  - validation issues beneath both
- Added presentation tests for:
  - initial placeholder state
  - selection-driven property population
  - shell selection propagation

## Behavior

- The panel is intentionally placeholder-backed for now.
- It shows clearly marked placeholder property rows until a real property projection exists.
- The view model keeps a clean selection boundary so future drag/drop or add actions can attach without reshaping the shell.

## Notes

- No domain logic was added.
- The panel remains presentation-safe even though the backend property projection is not implemented yet.
