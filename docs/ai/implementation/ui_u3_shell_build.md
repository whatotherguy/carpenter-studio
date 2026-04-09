# UI U3 Shell Build Notes

Date: 2026-04-08
Scope: `CabinetDesigner.Presentation`

## What Changed

- Replaced the bare canvas-only main window with a docked editor shell.
- Kept the existing canvas host centered and bound through `CanvasView`.
- Surfaced the current project commands in both the menu and toolbar.
- Added visible placeholder regions for catalog, property inspector, run summary, and issue feed.
- Added a structured bottom status bar using existing `ShellViewModel` display properties.

## Layout

- Top menu: File, Edit, View, Design, Tools, Help.
- Command strip: New, Open, Save, Close, Undo, Redo plus inline project name/path inputs.
- Left pane: catalog placeholder.
- Center pane: existing editor canvas host.
- Right pane: property inspector placeholder.
- Bottom left: run summary placeholder.
- Bottom right: issue panel placeholder.
- Bottom bar: current status, project summary, mode, and save state.

## Binding Notes

- Window title remains bound to `WindowTitle`.
- Canvas content remains bound to `CanvasView`.
- Command buttons and menu entries use the existing `NewProjectCommand`, `OpenProjectCommand`, `SaveCommand`, `CloseProjectCommand`, `UndoCommand`, and `RedoCommand`.
- Placeholder panels are populated from the existing string/list properties already exposed by `ShellViewModel`.

## Constraints Kept

- No business logic was added to code-behind.
- The view model is still disposed from `MainWindow` when the window closes.
- No new presentation services or view models were required for this shell step.
