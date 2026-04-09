# U20 — Unsaved-Changes Prompt on Close + SaveStateText Staleness Fix

## Context

Two related issues:

1. `ProjectService.CloseAsync()` silently closes without checking for unsaved changes.  No
   "Save before closing?" dialog is shown.  Users can lose work.

2. `ShellViewModel.SaveStateText` derives from `ActiveProject.HasUnsavedChanges`, but
   `ActiveProject` is only updated on explicit project events (`ProjectOpenedEvent`,
   `ProjectClosedEvent`).  After a design change (`DesignChangedEvent`) the save state badge stays
   "Saved" even though the design has changed.

## Files to Read First

- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`
- `src/CabinetDesigner.Application/Services/IProjectService.cs`
- `src/CabinetDesigner.Application/DTOs/ProjectSummaryDto.cs`
- `src/CabinetDesigner.Application/Events/ApplicationEvents.cs`
- `src/CabinetDesigner.Presentation/IDialogService.cs` (created in U16)

## Task

### 1. Fix `SaveStateText` staleness — subscribe to `DesignChangedEvent` in `ShellViewModel`

`ShellViewModel` currently subscribes to `ProjectOpenedEvent`, `ProjectClosedEvent`, and undo/redo
events.  Add a subscription to `DesignChangedEvent`:

In the constructor, after the existing subscriptions:
```csharp
_eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
```

In `Dispose()`:
```csharp
_eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
```

Add the handler:
```csharp
private void OnDesignChanged(DesignChangedEvent _)
{
    // Refresh the current project summary so HasUnsavedChanges is up-to-date
    SetActiveProject(_projectService.CurrentProject);
}
```

This refreshes `ActiveProject` from the service (which reflects the current unsaved state) whenever
the design changes, keeping `SaveStateText` accurate.

### 2. Add an "unsaved changes" prompt before `CloseProjectAsync`

`ShellViewModel` gets `IDialogService` injected (from U16).  Use it in `CloseProjectAsync`:

```csharp
private async Task CloseProjectAsync()
{
    if (ActiveProject?.HasUnsavedChanges == true)
    {
        var save = _dialogService.ShowYesNoDialog(
            "Unsaved Changes",
            $"'{ActiveProject.Name}' has unsaved changes. Save before closing?");

        if (save)
        {
            await _projectService.SaveAsync().ConfigureAwait(true);
        }
    }

    await _projectService.CloseAsync().ConfigureAwait(true);
}
```

### 3. Add an "unsaved changes" prompt on window close

In `CabinetDesigner.Presentation.MainWindow.xaml.cs`, override `OnClosing` to intercept the window
close event and ask for confirmation when there are unsaved changes:

```csharp
protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
{
    if (_viewModel.ActiveProject?.HasUnsavedChanges == true)
    {
        var result = System.Windows.MessageBox.Show(
            $"'{_viewModel.ActiveProject.Name}' has unsaved changes. Save before exiting?",
            "Unsaved Changes",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            // Fire-and-forget save on window close — use a synchronous path if available.
            // Since we cannot await here, call SaveAsync and let it run.
            _ = _viewModel.SaveCommand.ExecuteAsync();
            // Give it a moment to complete (best-effort on close)
        }
    }

    base.OnClosing(e);
}
```

Note: Proper async save on window close is hard to do correctly in WPF (you cannot async/await in
`OnClosing`).  The above pattern fires the save and lets WPF close the window; if the save is fast
(local SQLite) this works in practice.  Add a comment acknowledging this limitation.

`_viewModel.ActiveProject` must be publicly accessible — it already is (`public ProjectSummaryDto? ActiveProject`).
`_viewModel.SaveCommand` is `public AsyncRelayCommand SaveCommand`.

### 4. Verify `ProjectSummaryDto.HasUnsavedChanges` is updated correctly by the service

Read `IProjectService.CurrentProject` getter and confirm it returns a fresh `ProjectSummaryDto`
with an up-to-date `HasUnsavedChanges` flag after a design command is applied.  If
`HasUnsavedChanges` is computed at query time rather than stored, no changes are needed.  If it is
stale (set only at open/save time), that is a deeper application service bug — document it as a
known limitation and do not fix it here.

## Requirements

- Do not modify `IProjectService` or any application/domain layer — only presentation layer.
- If `IDialogService` is not yet injected (U16 not done), add the injection anyway with a null
  check guard: `_dialogService?.ShowYesNoDialog(...)`.
- `OnClosing` in `MainWindow.xaml.cs` is the correct WPF hook — do not use `Closing` event in XAML.

## End State

- What is now usable: Closing a project or window with unsaved changes prompts to save.
  `SaveStateText` badge accurately reflects design changes.
- What is still missing: Panel resizing, multi-select, loading indicators (quality-of-life items).
- Next prompt: U21 — GridSplitters, loading indicators, Ctrl+click multi-select.
