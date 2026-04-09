# U15 — Wire Keyboard Shortcuts (Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+W, Ctrl+Z, Ctrl+Y)

## Context

`CabinetDesigner.Presentation.MainWindow.xaml` shows `InputGestureText="Ctrl+N"` labels on menu
items, but no `InputBindings` or `CommandBindings` are defined anywhere.  The shortcuts are
cosmetic — pressing Ctrl+S does nothing.

## Files to Read First

- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `src/CabinetDesigner.Presentation/MainWindow.xaml.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` (command properties)

## Task

### 1. Add `Window.InputBindings` to `CabinetDesigner.Presentation.MainWindow.xaml`

Insert the following block immediately before `<DockPanel LastChildFill="True">` (after the
`Window.Resources` closing tag):

```xml
<Window.InputBindings>
    <KeyBinding Key="N" Modifiers="Ctrl" Command="{Binding NewProjectCommand}" />
    <KeyBinding Key="O" Modifiers="Ctrl" Command="{Binding OpenProjectCommand}" />
    <KeyBinding Key="S" Modifiers="Ctrl" Command="{Binding SaveCommand}" />
    <KeyBinding Key="W" Modifiers="Ctrl" Command="{Binding CloseProjectCommand}" />
    <KeyBinding Key="Z" Modifiers="Ctrl" Command="{Binding UndoCommand}" />
    <KeyBinding Key="Y" Modifiers="Ctrl" Command="{Binding RedoCommand}" />
</Window.InputBindings>
```

These bind directly to the commands already exposed on `ShellViewModel`, so no code-behind changes
are needed.

### 2. Verify `InputGestureText` labels match

Confirm the existing `MenuItem` entries in `MainWindow.xaml` use consistent text:
- `Ctrl+N` → `NewProjectCommand`
- `Ctrl+O` → `OpenProjectCommand`
- `Ctrl+S` → `SaveCommand`
- `Ctrl+W` → `CloseProjectCommand`
- `Ctrl+Z` → `UndoCommand`
- `Ctrl+Y` → `RedoCommand`

Do not change the menu content — this step is just a verification.

## Requirements

- Changes are XAML-only; no `.cs` changes needed.
- Do not add `CommandBinding` entries — `ICommand`-based `KeyBinding` is sufficient and avoids
  boilerplate.
- The `NewProjectCommand` and `OpenProjectCommand` have `CanExecute` guards (`PendingProjectName`
  and `PendingProjectFilePath` must be non-empty).  After U16 (file dialogs), Ctrl+O will open a
  dialog that pre-fills `PendingProjectFilePath`.  For now the binding is correct and will be a
  no-op when the text box is empty.

## End State

- What is now usable: Ctrl+S saves, Ctrl+Z/Y undo/redo, Ctrl+W closes project.
- What is still missing: File dialogs (Ctrl+O/N still need a text box until U16).
- Next prompt: U16 — Add file open/save dialogs.
