# U16 — Add File Open/Save Dialogs

## Context

`ShellViewModel.OpenProjectAsync` requires `PendingProjectFilePath` to be typed manually into a
raw `TextBox`.  There is no `OpenFileDialog` or `SaveFileDialog` anywhere.  For "New Project",
`PendingProjectName` is a text box for the project name but the file path also needs to be chosen.
The `Presentation/MainWindow.xaml` does not expose these text boxes at all — the menu items for
New/Open/Save simply invoke commands and expect the ViewModel to have a valid path already.

The fix is to show `OpenFileDialog` / `SaveFileDialog` before invoking the underlying service,
catching the user's file selection and wiring it into the project service call.

## Files to Read First

- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `src/CabinetDesigner.App/MainWindow.xaml` (for context on PendingProjectFilePath text box)
- `src/CabinetDesigner.Application/Services/IProjectService.cs`

## Task

### 1. Add a dialog service interface to the Presentation layer

Create `src/CabinetDesigner.Presentation/IDialogService.cs`:

```csharp
namespace CabinetDesigner.Presentation;

public interface IDialogService
{
    /// <summary>Returns the chosen file path, or null if the user cancelled.</summary>
    string? ShowOpenFileDialog(string title, string filter);

    /// <summary>Returns the chosen file path, or null if the user cancelled.</summary>
    string? ShowSaveFileDialog(string title, string filter, string defaultFileName);

    /// <summary>Returns true if the user chose Yes.</summary>
    bool ShowYesNoDialog(string title, string message);
}
```

### 2. Add a WPF implementation in the App layer

Create `src/CabinetDesigner.App/WpfDialogService.cs`:

```csharp
using System.Windows;
using CabinetDesigner.Presentation;
using Microsoft.Win32;

namespace CabinetDesigner.App;

public sealed class WpfDialogService : IDialogService
{
    public string? ShowOpenFileDialog(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool ShowYesNoDialog(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;
}
```

### 3. Register `IDialogService` in `App.xaml.cs`

In `ConfigureServices`, after `services.AddPresentationServices()`:

```csharp
services.AddScoped<IDialogService, WpfDialogService>();
```

### 4. Inject `IDialogService` into `ShellViewModel`

Add `IDialogService dialogService` as the last constructor parameter of `ShellViewModel`.
Store it in a field `_dialogService`.

### 5. Rewrite `CreateProjectAsync` to prompt for a save path

```csharp
private async Task CreateProjectAsync()
{
    var path = _dialogService.ShowSaveFileDialog(
        "Create New Project",
        "Carpenter Studio Project (*.db)|*.db|All files (*.*)|*.*",
        string.IsNullOrWhiteSpace(PendingProjectName) ? "project.db" : $"{PendingProjectName}.db");

    if (path is null)
        return;

    PendingProjectFilePath = path;
    ActiveProject = await _projectService.CreateProjectAsync(PendingProjectName, path).ConfigureAwait(true);
}
```

If `IProjectService.CreateProjectAsync` currently takes only a name (no path), check its signature.
If it uses the database path configured at startup (single-file app), then `CreateProjectAsync` does
not need a path argument — just use it as-is and skip the `ShowSaveFileDialog` for now.  In that
case the `CreateProjectAsync` command just creates a project named `PendingProjectName` with no
dialog.  Annotate the method with a `// TODO: per-project file path when multi-project is supported`
comment and leave the dialog call for a future prompt.

### 6. Rewrite `OpenProjectAsync` to show an open dialog

```csharp
private async Task OpenProjectAsync()
{
    var path = _dialogService.ShowOpenFileDialog(
        "Open Project",
        "Carpenter Studio Project (*.db)|*.db|All files (*.*)|*.*");

    if (path is null)
        return;

    PendingProjectFilePath = path;
    ActiveProject = await _projectService.OpenProjectAsync(path).ConfigureAwait(true);
}
```

Remove the `CanExecute` guard that blocks the command when `PendingProjectFilePath` is empty —
the dialog now provides the path.  Change the `CanExecute` lambda for `OpenProjectCommand` to
`() => true` (or remove it entirely).

### 7. Update `NewProjectCommand` CanExecute

`NewProjectCommand` currently blocks when `PendingProjectName` is empty.  After this change the
dialog replaces the text box.  However keep the guard in place — if the user has cleared the name
field, show the dialog but use `"New Project"` as the default name.  The `CanExecute` guard for
`NewProjectCommand` can be relaxed to `() => true` as well, since the dialog will allow naming.
Update the guard accordingly and remove `OnPropertyChanged(nameof(NewProjectCommand)...` from
`PendingProjectName`'s setter if it becomes unconditional.

### 8. Update `CanExecute` for `OpenProjectCommand` in `PendingProjectFilePath` setter

`PendingProjectFilePath`'s setter calls `OpenProjectCommand.NotifyCanExecuteChanged()`.  After
removing the path guard, this call is a no-op but harmless — leave it.

## Requirements

- `IDialogService` must live in `CabinetDesigner.Presentation` (not the App layer) so `ShellViewModel`
  can depend on it without referencing WPF.
- `WpfDialogService` must live in `CabinetDesigner.App` (WPF-specific).
- Do not use `Dispatcher` or `Application.Current` in `WpfDialogService`.
- Do not remove `PendingProjectName` or `PendingProjectFilePath` properties from `ShellViewModel` —
  they may still be referenced from tests or the App/MainWindow toolbar.

## End State

- What is now usable: Ctrl+O opens a file browser; New Project shows a save dialog.
- What is still missing: Canvas pan/zoom, adding cabinets from the catalog.
- Next prompt: U17 — Canvas pan/zoom via mouse wheel and middle-click drag.
