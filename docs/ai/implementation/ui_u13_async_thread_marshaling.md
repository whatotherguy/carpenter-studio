# U13 — Fix Async/UI Thread Marshaling in AsyncRelayCommand

## Context

`AsyncRelayCommand.Execute` uses `ConfigureAwait(false)` (line 21), so all continuations including
the `finally` block run on a thread pool thread.  `NotifyCanExecuteChanged()` fires
`CanExecuteChanged` from the thread pool, which causes WPF binding exceptions or silent failures.
The same problem propagates into every async command in `ShellViewModel` — after
`CreateProjectAsync`, `OpenProjectAsync`, `SaveAsync`, and `CloseProjectAsync` return, the
`ActiveProject` setter calls `OnPropertyChanged` from a background thread.

## Files to Read First

- `src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs`
- `src/CabinetDesigner.Presentation/ObservableObject.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` (lines 158–177)

## Task

### 1. Capture `SynchronizationContext` in `AsyncRelayCommand`

In `AsyncRelayCommand.cs`:

- Add a `private readonly SynchronizationContext? _synchronizationContext;` field.
- In the constructor, capture `SynchronizationContext.Current` and assign it to the field.
- Add a private helper method `PostToUiThread(Action action)`:
  ```csharp
  private void PostToUiThread(Action action)
  {
      if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
          _synchronizationContext.Post(_ => action(), null);
      else
          action();
  }
  ```
- In `ExecuteAsync`, wrap both `NotifyCanExecuteChanged()` calls with `PostToUiThread`:
  ```csharp
  _isExecuting = true;
  PostToUiThread(NotifyCanExecuteChanged);
  try
  {
      await _executeAsync().ConfigureAwait(false);
  }
  finally
  {
      _isExecuting = false;
      PostToUiThread(NotifyCanExecuteChanged);
  }
  ```

### 2. Fix `ShellViewModel` async methods — marshal property changes back to UI thread

In `ShellViewModel.cs`, the three methods that write `ActiveProject` or fire `OnPropertyChanged`
after `await` need their continuations to run on the UI thread.

Change the three `ConfigureAwait(false)` calls to `ConfigureAwait(true)`:

```csharp
private async Task CreateProjectAsync()
{
    ActiveProject = await _projectService.CreateProjectAsync(PendingProjectName).ConfigureAwait(true);
}

private async Task OpenProjectAsync()
{
    ActiveProject = await _projectService.OpenProjectAsync(PendingProjectFilePath).ConfigureAwait(true);
}

private async Task SaveAsync()
{
    await _projectService.SaveAsync().ConfigureAwait(true);
    SetActiveProject(_projectService.CurrentProject);
}
```

`CloseProjectAsync` does not write any properties directly, so it can stay as-is (`ConfigureAwait(false)`
is fine there).

### 3. Fix `PropertyInspectorViewModel` — `CommitNominalWidthEditAsync`

In `PropertyInspectorViewModel.cs`, `CommitNominalWidthEditAsync` calls
`_runService.ResizeCabinetAsync(...).ConfigureAwait(false)` and then writes several properties.
Change to `ConfigureAwait(true)`.

### 4. Fix `EditorCanvasViewModel` — async methods

In `EditorCanvasViewModel.cs`, `AddCabinetToRunAsync` and `MoveCabinetAsync` both write
`StatusMessage` after `await ... ConfigureAwait(false)`.  Change both to `ConfigureAwait(true)`.

## Requirements

- Do not change anything else in these files.
- Do not introduce a `Dispatcher` reference — the `SynchronizationContext` approach keeps the
  commands testable without WPF.
- All `NotifyCanExecuteChanged` calls that happen synchronously (not in a finally after await)
  do not need wrapping — they already run on the UI thread.
- The `RelayCommand` and `RelayCommand<T>` classes do not need changes.

## End State

- What is now fixed: WPF binding exceptions from cross-thread property changes are eliminated.
- What is still missing: Mouse interaction, keyboard shortcuts.
- Next prompt: U14 — Wire mouse events to the canvas for click-to-select and hover.
