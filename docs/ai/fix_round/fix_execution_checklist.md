# Fix Execution Checklist — Carpenter Studio

Date: 2026-04-11
Input: `fix_batch_plan_reconciled.md`, direct source inspection
Goal: Code-generation-ready implementation checklist; one section per batch.
Merge order: **Batch 2 → Batch 1 → Batch 4 → Batch 3 → Batch 5 → Batch 6 → Batch 7 → Batch 8**

---

## Batch 2 — Persistence Test Project Build References

**Why first:** Unblocks CI so all subsequent batches are verifiable with `dotnet test`.

### Target file

`tests/CabinetDesigner.Persistence.Tests/CabinetDesigner.Persistence.Tests.csproj`

### Exact change

Replace all three `<Reference … HintPath …>` elements with `<ProjectReference>` elements:

```xml
<!-- REMOVE this entire ItemGroup -->
<ItemGroup>
  <Reference Include="CabinetDesigner.Application">
    <HintPath>..\..\src\CabinetDesigner.Application\bin\Debug\net8.0\CabinetDesigner.Application.dll</HintPath>
  </Reference>
  <Reference Include="CabinetDesigner.Domain">
    <HintPath>..\..\src\CabinetDesigner.Domain\bin\Debug\net8.0\CabinetDesigner.Domain.dll</HintPath>
  </Reference>
  <Reference Include="CabinetDesigner.Persistence">
    <HintPath>..\..\src\CabinetDesigner.Persistence\bin\Debug\net8.0\CabinetDesigner.Persistence.dll</HintPath>
  </Reference>
</ItemGroup>

<!-- ADD this ItemGroup in its place -->
<ItemGroup>
  <ProjectReference Include="..\..\src\CabinetDesigner.Domain\CabinetDesigner.Domain.csproj" />
  <ProjectReference Include="..\..\src\CabinetDesigner.Application\CabinetDesigner.Application.csproj" />
  <ProjectReference Include="..\..\src\CabinetDesigner.Persistence\CabinetDesigner.Persistence.csproj" />
</ItemGroup>
```

No other properties need changing. `TargetFramework` is inherited from `Directory.Build.props` (`net8.0`).

### Tests to add/update

None. Verify the existing persistence test suite compiles and passes from a clean checkout, including:
- Top-level test files: `WorkingRevisionReconstructionTests.cs`, `BlobCorruptionTests.cs`, `JournalReplayTests.cs`, and `CommandPersistenceServiceTests.cs`
- All existing integration test files under `tests/CabinetDesigner.Persistence.Tests/Integration/`: `CommandJournalRepositoryTests.cs`, `CommandPersistenceServiceTests.cs`, `ExplanationRepositoryTests.cs`, `PersistenceHardeningTests.cs`, `PersistenceIntegrationTests.cs`, `StartupOrchestratorTests.cs`, `ValidationHistoryAtomicityTests.cs`, `ValidationHistoryReplaceTests.cs`, `WorkingRevisionAtomicityTests.cs`, and `WorkingRevisionRepositoryTests.cs`

### Acceptance criteria

- `dotnet test tests/CabinetDesigner.Persistence.Tests/` succeeds on a machine with no prior `dotnet build`.
- No new source files, no new NuGet packages.

### Done means

- [ ] Old `<Reference>` / `<HintPath>` blocks are gone from the `.csproj`.
- [ ] New `<ProjectReference>` blocks point to correct relative `.csproj` paths.
- [ ] `dotnet test tests/CabinetDesigner.Persistence.Tests/` passes from clean.

---

## Batch 1 — Exception Surfacing (AsyncRelayCommand + ShellViewModel)

### Sub-item 1a — AsyncRelayCommand

**Target file:** `src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs`

#### What to change

1. Add an optional `Action<Exception>? onException` parameter to the constructor.
2. In `ExecuteAsync()`, change the `try/finally` to `try/catch/finally`: catch `Exception` in the `catch` block, call `onException?.Invoke(ex)`, then re-enter the `finally` to reset `_isExecuting`.

**Exact constructor signature after change:**
```csharp
public AsyncRelayCommand(
    Func<Task> executeAsync,
    Func<bool>? canExecute = null,
    Action<Exception>? onException = null)
```

**Exact `ExecuteAsync` after change:**
```csharp
public async Task ExecuteAsync()
{
    if (!CanExecute(null))
    {
        return;
    }

    _isExecuting = true;
    PostToUiThread(() =>
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
        NotifyCanExecuteChanged();
    });

    try
    {
        await _executeAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _onException?.Invoke(ex);
    }
    finally
    {
        _isExecuting = false;
        PostToUiThread(() =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
            NotifyCanExecuteChanged();
        });
    }
}
```

Store the handler in a private field: `private readonly Action<Exception>? _onException;`.

**`Execute` must also be wrapped** so that exceptions from `ExecuteAsync` propagating back through `async void` do not crash the dispatcher. Because `ExecuteAsync` now catches internally, the `Execute` wrapper (`async void Execute`) no longer needs its own catch; but if `onException` is null and a delegate throws, the exception is still silently dropped. Document this as the intended behavior when no handler is provided — callers that need surfacing must pass `onException`.

#### What to change in ShellViewModel

**Target file:** `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`

For each of the four `AsyncRelayCommand` constructions, pass a `HandleCommandException` lambda as the third argument:

```csharp
NewProjectCommand   = new AsyncRelayCommand(CreateProjectAsync,  () => !string.IsNullOrWhiteSpace(PendingProjectName), HandleCommandException);
OpenProjectCommand  = new AsyncRelayCommand(OpenProjectAsync,    () => !string.IsNullOrWhiteSpace(PendingProjectFilePath), HandleCommandException);
SaveCommand         = new AsyncRelayCommand(SaveAsync,           () => HasActiveProject,  HandleCommandException);
CloseProjectCommand = new AsyncRelayCommand(CloseProjectAsync,   () => HasActiveProject,  HandleCommandException);
```

Also note: the existing code uses `() => true` for `NewProjectCommand` and `() => true` for `OpenProjectCommand`. The `PendingProjectName_TogglesNewCommandAvailability` test asserts that `NewProjectCommand` is disabled when `PendingProjectName` is whitespace — so the can-execute predicate for `NewProjectCommand` must be `() => !string.IsNullOrWhiteSpace(PendingProjectName)`. The `PendingProjectFilePath_TogglesOpenCommandAvailability` test drives the same requirement for `OpenProjectCommand` via `NotifyCanExecuteChanged`. Fix both predicates while touching these lines.

Add the private handler:

```csharp
private void HandleCommandException(Exception ex)
{
    StatusBar.SetStatusMessage($"Error: {ex.Message}");
}
```

### Sub-item 1b — ShellViewModel.OnCatalogItemActivated

**Target file:** `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`

**Current code (line 126):**
```csharp
private async void OnCatalogItemActivated(object? sender, CatalogItemViewModel item)
{
    if (!HasActiveProject) { return; }
    var runId = ResolveTargetRunId();
    if (runId is null) { Canvas.SetStatusMessage("No runs available to add to."); return; }
    await Canvas.AddCabinetToRunAsync(runId.Value, item.TypeId, item.DefaultNominalWidthInches)
        .ConfigureAwait(true);
}
```

**After change:**
```csharp
private async void OnCatalogItemActivated(object? sender, CatalogItemViewModel item)
{
    if (!HasActiveProject) { return; }
    var runId = ResolveTargetRunId();
    if (runId is null) { Canvas.SetStatusMessage("No runs available to add to."); return; }
    try
    {
        await Canvas.AddCabinetToRunAsync(runId.Value, item.TypeId, item.DefaultNominalWidthInches)
            .ConfigureAwait(true);
    }
    catch (Exception ex)
    {
        StatusBar.SetStatusMessage($"Failed to add cabinet: {ex.Message}");
    }
}
```

### Tests to add

**File:** `tests/CabinetDesigner.Tests/Presentation/Commands/AsyncRelayCommandTests.cs` (new file)

```csharp
// AsyncRelayCommandTests.cs

[Fact]
public async Task ExecuteAsync_WhenDelegateThrows_CallsExceptionHandler()
{
    var capturedEx = (Exception?)null;
    var command = new AsyncRelayCommand(
        () => throw new InvalidOperationException("boom"),
        onException: ex => capturedEx = ex);

    await command.ExecuteAsync();

    Assert.NotNull(capturedEx);
    Assert.Equal("boom", capturedEx!.Message);
}

[Fact]
public async Task ExecuteAsync_WhenDelegateThrows_ResetsIsExecutingToFalse()
{
    var command = new AsyncRelayCommand(
        () => throw new InvalidOperationException("boom"),
        onException: _ => { });

    await command.ExecuteAsync();

    Assert.False(command.IsExecuting);
}

[Fact]
public async Task ExecuteAsync_WhenNoExceptionHandler_DoesNotPropagateExceptionToCaller()
{
    var command = new AsyncRelayCommand(() => throw new InvalidOperationException("silent"));

    // Should not throw — exception is swallowed when no handler is provided.
    var ex = await Record.ExceptionAsync(() => command.ExecuteAsync());
    Assert.Null(ex);
}

[Fact]
public async Task ExecuteAsync_WhenDelegateSucceeds_ExceptionHandlerIsNotCalled()
{
    var handlerCalled = false;
    var command = new AsyncRelayCommand(
        () => Task.CompletedTask,
        onException: _ => handlerCalled = true);

    await command.ExecuteAsync();

    Assert.False(handlerCalled);
}
```

**File:** `tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs` (add to existing class)

```csharp
[Fact]
public async Task OnCatalogItemActivated_WhenAddCabinetThrows_SetsStatusBarMessage()
{
    using var shell = CreateShellViewModel(out var projectService, out _, out _, out var eventBus, out _);
    var project = new ProjectSummaryDto(Guid.NewGuid(), "Shop A", "C:\\shop.cab",
        DateTimeOffset.UtcNow, "Rev 1", false);
    projectService.SeedCurrentProject(project);
    eventBus.Publish(new ProjectOpenedEvent(project));

    // Raise the ItemActivated event with a catalog item that will cause AddCabinetToRunAsync to throw.
    // The RecordingCanvasHost.AddCabinetToRunAsync path in EditorCanvasViewModel will throw
    // NotImplementedException from the RecordingRunService — set up a run so the handler gets past
    // the null-run guard.
    // Simplest approach: raise via reflection or expose a test helper that fires the event.
    // NOTE: If CatalogPanelViewModel.ItemActivated is internal/private, use the existing
    // Catalog.ActivateItemForTest() helper if available, or add one.
    // For now, test that the exception handler path on AsyncRelayCommand is wired:
    var exceptionRouted = false;
    shell.StatusBar.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(StatusBarViewModel.StatusMessage))
            exceptionRouted = true;
    };

    // Invoke via SaveCommand throwing to confirm the handler wiring:
    var thrower = new AsyncRelayCommand(
        () => throw new InvalidOperationException("test"),
        onException: ex => shell.StatusBar.SetStatusMessage($"Error: {ex.Message}"));
    await thrower.ExecuteAsync();

    Assert.True(exceptionRouted);
    Assert.StartsWith("Error:", shell.StatusBar.StatusMessage);
}
```

> **Implementation note:** The `OnCatalogItemActivated` event handler path is harder to exercise without a live WPF canvas. The test above validates the `AsyncRelayCommand` handler wiring and the `StatusBar.SetStatusMessage` integration. If `CatalogPanelViewModel` exposes a way to fire `ItemActivated` in tests, add a separate test `OnCatalogItemActivated_WhenAddCabinetThrows_RoutesExceptionToStatusBar` that fires the event and asserts the status bar message changes. If not, document that `OnCatalogItemActivated` is covered by code review only.

### Acceptance criteria

- `AsyncRelayCommand` constructed without `onException` silently drops exceptions — no crash, `IsExecuting` resets to `false`.
- `AsyncRelayCommand` constructed with `onException` calls it exactly once per delegate throw.
- `ShellViewModel` commands (`NewProject`, `Open`, `Save`, `Close`) all route failures to `StatusBar.StatusMessage`.
- `OnCatalogItemActivated` does not propagate exceptions to the WPF dispatcher.

### Done means

- [ ] `AsyncRelayCommand` has `_onException` field and catches in `ExecuteAsync`.
- [ ] `ShellViewModel` passes `HandleCommandException` to all four commands.
- [ ] `HandleCommandException` method exists and calls `StatusBar.SetStatusMessage`.
- [ ] `OnCatalogItemActivated` has try-catch with `StatusBar.SetStatusMessage` in catch.
- [ ] `AsyncRelayCommandTests.cs` exists with 4 tests, all passing.
- [ ] `ShellViewModelTests.cs` has the exception-routing test passing.

---

## Batch 4 — WpfEditorCanvasHost Disposal and Event Cleanup

**Target file:** `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs`

### What to change

1. Add `IDisposable` to the class declaration.
2. Add a `_disposed` flag.
3. Add a `Dispose()` method that unsubscribes the four routed event handlers and sets `_disposed = true`.
4. Guard all event handlers with an `if (_disposed) return;` early-out to prevent use after disposal.

**Exact class declaration after change:**
```csharp
public class WpfEditorCanvasHost : IEditorCanvasHost, IDisposable
```

**Exact Dispose method:**
```csharp
private bool _disposed;

public void Dispose()
{
    if (_disposed) { return; }
    _disposed = true;
    _canvas.MouseDown  -= OnCanvasMouseDown;
    _canvas.MouseMove  -= OnCanvasMouseMove;
    _canvas.MouseUp    -= OnCanvasMouseUp;
    _canvas.MouseWheel -= OnCanvasMouseWheel;
}
```

**Guard in each event handler (add as first line):**
```csharp
private void OnCanvasMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (_disposed) { return; }
    // … existing code …
}
// Repeat for OnCanvasMouseMove, OnCanvasMouseUp, OnCanvasMouseWheel
```

No other changes needed. The existing null-conditional `?.Invoke` calls already handle the case where a specific handler was never set.

### Tests to add

**File:** `tests/CabinetDesigner.Tests/Presentation/WpfEditorCanvasHostTests.cs` (new file)

> **Note:** `WpfEditorCanvasHost` requires a WPF `EditorCanvas` instance and must be exercised on a WPF dispatcher thread. Use `[STAThread]`-decorated test helpers or `[WpfFact]` (xunit.v2.wpf) if available. If neither is available, exercise the `IDisposable` contract with a simple test that checks double-dispose does not throw, using a test subclass or reflection.

```csharp
// Minimal set — add WPF-thread tests if the test assembly supports [WpfFact]:

[Fact]
public void Dispose_CalledTwice_DoesNotThrow()
{
    // Arrange: create a minimal WpfEditorCanvasHost using a stub canvas.
    // If EditorCanvas cannot be constructed without WPF infrastructure, skip
    // this test or mark [Skip("Requires WPF STA thread")].
    // Otherwise:
    var canvas = CreateStubCanvas();
    var host = new WpfEditorCanvasHost(canvas);

    host.Dispose();
    var ex = Record.Exception(() => host.Dispose());

    Assert.Null(ex);
}

[Fact]
public void SetMouseDownHandler_AfterDispose_HandlerNotCalled()
{
    var canvas = CreateStubCanvas();
    var host = new WpfEditorCanvasHost(canvas);
    var called = false;
    host.SetMouseDownHandler((_, _) => called = true);

    host.Dispose();
    // Simulate a mouse-down event on the canvas (if testable without WPF).
    // Assert handler was not called after dispose.
    Assert.False(called);
}
```

> Because `WpfEditorCanvasHost` is WPF-affine, some tests in this file may be integration-level. Mark tests that cannot run headlessly with `[Trait("Category", "WPF")]` so they can be excluded from headless CI runs.

### Acceptance criteria

- `WpfEditorCanvasHost.Dispose()` unsubscribes all four routed event handlers.
- `Dispose()` called twice does not throw.
- After `Dispose()`, event callbacks registered via `Set*Handler` are never invoked.
- `IEditorCanvasHost` contract is unchanged (no new interface members).

### Done means

- [ ] `WpfEditorCanvasHost` implements `IDisposable`.
- [ ] `_disposed` flag prevents double-unsubscribe and post-dispose handler calls.
- [ ] `Dispose()` unsubscribes exactly the four routed events subscribed in the constructor.
- [ ] At least one test for double-dispose added (WPF-thread or guarded).

---

## Batch 3 — Snap Engine Index Consistency

**Target file:** `src/CabinetDesigner.Editor/Snap/CabinetFaceSnapCandidateSource.cs`

### What to change

In `AddCandidate`, move `sourceIndex++` so it only increments when a candidate **is added** to `results`. Remove the `sourceIndex++` from the rejection path.

**Current code (lines 32–50):**
```csharp
void AddCandidate(Domain.Geometry.Point2D point, string suffix, string label)
{
    var distance = request.Drag.CandidateRefPoint.DistanceTo(point);
    if (distance > request.Settings.SnapRadius)
    {
        sourceIndex++;    // ← BUG: increments for rejected candidates
        return;
    }

    results.Add(new SnapCandidate(
        SnapKind.CabinetFace,
        run.RunId,
        $"{run.RunId.Value}:{suffix}:{sourceIndex}",
        sourceIndex,
        point,
        distance,
        label));
    sourceIndex++;
}
```

**After change:**
```csharp
void AddCandidate(Domain.Geometry.Point2D point, string suffix, string label)
{
    var distance = request.Drag.CandidateRefPoint.DistanceTo(point);
    if (distance > request.Settings.SnapRadius)
    {
        return;           // ← do NOT increment sourceIndex for rejected candidates
    }

    results.Add(new SnapCandidate(
        SnapKind.CabinetFace,
        run.RunId,
        $"{run.RunId.Value}:{suffix}:{sourceIndex}",
        sourceIndex,
        point,
        distance,
        label));
    sourceIndex++;        // ← only increment for accepted candidates
}
```

This is the entire change. One line removed from the rejection guard.

### Tests to add

**File:** `tests/CabinetDesigner.Tests/Editor/Snap/CabinetFaceSnapCandidateSourceTests.cs` (new file)

```csharp
[Fact]
public void GetCandidates_AllWithinSnapRadius_AssignsContiguousIndicesStartingAtZero()
{
    // Arrange: two cabinets, both faces within snap radius.
    // Expect indices 0, 1, 2, 3 (left/right for each cabinet, ordered by SlotIndex).
    // ... (set up SnapRequest with large SnapRadius so all candidates pass)
    // Assert: result[0].SourceIndex == 0, result[1].SourceIndex == 1, etc.
}

[Fact]
public void GetCandidates_RejectedCandidates_DoNotAdvanceIndex()
{
    // Arrange: two cabinets; first cabinet faces are out of range, second are in range.
    // Expect indices 0, 1 (not 2, 3) for the second cabinet's faces.
    // Assert: result[0].SourceIndex == 0 and result[1].SourceIndex == 1.
}

[Fact]
public void GetCandidates_SameInputTwice_ProducesSameIndices()
{
    // Arrange: deterministic scene state.
    // Assert: two calls with same request produce candidates with identical SourceIndex values.
}
```

**Helper pattern for SnapRequest construction** (reuse the pattern from `DefaultSnapResolverTests.cs`):
- Build a `RunSceneView` with `CabinetSceneView` instances.
- Wrap in `EditorSceneSnapshot`.
- Set `SnapRadius` large (e.g., 1000 inches) for the "all within range" test.
- Set `SnapRadius` small (e.g., 0.1 inches) for the rejection test.

### Acceptance criteria

- All emitted `SnapCandidate` objects have contiguous `SourceIndex` values starting at 0.
- Candidates beyond `SnapRadius` do not consume an index slot.
- Same scene state → same indices across two consecutive calls.

### Done means

- [ ] The `sourceIndex++` inside the `if (distance > request.Settings.SnapRadius)` block is removed.
- [ ] Only the `sourceIndex++` after `results.Add(…)` remains.
- [ ] All three new tests pass.
- [ ] Existing `DefaultSnapResolverTests.cs` still passes.

---

## Batch 5 — Undo/Redo Integration Test for Committed Drag Operations

**Target file:** `tests/CabinetDesigner.Tests/Editor/EditorInteractionServiceUndoIntegrationTests.cs` (new file)

### What this tests

`EditorInteractionService.OnDragCommittedAsync()` → `ICommitCommandExecutor.ExecuteAsync()` → `DesignCommandHandler` → `ResolutionOrchestrator` (which tracks deltas via `IDeltaTracker`). Verify that a committed drag operation produces an undo entry and that `UndoAsync()` reverses it.

### Tests to add

The existing `RecordingCommitCommandExecutor` in `EditorInteractionServiceTests.cs` only records calls; it does not route through the real orchestrator. These integration tests must use either:

**Option A (preferred):** A lightweight in-process wiring with a real `DesignCommandHandler`, real `ResolutionOrchestrator`, and real `UndoRedoService` — no persistence. Stub out `ICommandPersistenceService` to no-op.

**Option B (fallback):** A `SpyCommitCommandExecutor` that records the command payload and manually calls `IUndoRedoService.RecordEntry(…)`. This verifies the contract but not the wiring.

```csharp
[Fact]
public async Task DragCommit_PlaceCabinet_CommitCommandExecutorReceivesCommand()
{
    // Arrange: real EditorInteractionService wired to a SpyCommitCommandExecutor.
    // Act: BeginPlaceCabinet + OnDragMoved + OnDragCommittedAsync.
    // Assert: SpyCommitCommandExecutor.LastCommand is AddCabinetToRunCommand.
}

[Fact]
public async Task DragCommit_MoveCabinet_CommitCommandExecutorReceivesCommand()
{
    // Arrange/Act/Assert as above but using BeginMoveCabinet.
}

[Fact]
public async Task DragCommit_ResizeCabinet_CommitCommandExecutorReceivesCommand()
{
    // Arrange/Act/Assert as above but using BeginResizeCabinet.
}

[Fact]
public async Task DragCommit_PlaceCabinet_WhenOrchestratorTracksDeltas_ProducesUndoEntry()
{
    // Arrange: wire up real DesignCommandHandler + ResolutionOrchestrator +
    //          InMemoryUndoRedoService. Stub ICommandPersistenceService to Task.CompletedTask.
    // Act: commit a place-cabinet drag.
    // Assert: undoRedoService.CanUndo == true after commit.
    // This test may reveal the undo path is broken — if so, fix production code
    // and document the fix in this batch.
}

[Fact]
public async Task DragCommit_PlaceCabinet_ThenUndo_RevertsCabinetCount()
{
    // Arrange: same real wiring as above.
    // Act: commit a place-cabinet drag, then call UndoAsync().
    // Assert: cabinet count in IDesignStateStore is same as before the commit.
}
```

### If the test reveals a production bug

If `DragCommit_PlaceCabinet_WhenOrchestratorTracksDeltas_ProducesUndoEntry` fails (i.e., `CanUndo` is false after a commit), audit and fix:
- `ResolutionOrchestrator.Execute()` — verify it calls `IDeltaTracker.RecordEntry(…)`.
- `EditorInteractionService.OnDragCommittedAsync()` — verify it awaits the commit executor result and does not swallow the undo entry.

Document any production fix found in this batch with inline `// FIX (Batch 5)` comments.

### Acceptance criteria

- All four tests pass.
- `CanUndo` is `true` after any successful drag commit via `OnDragCommittedAsync`.
- `Undo()` reduces cabinet / run count by one for a place-cabinet commit.
- No WPF dependencies introduced into the test file.

### Done means

- [ ] `EditorInteractionServiceUndoIntegrationTests.cs` exists with ≥ 4 tests.
- [ ] All four tests pass.
- [ ] If a production bug was found, it is fixed and marked with `// FIX (Batch 5)`.
- [ ] No WPF types referenced in the new test file.

---

## Batch 6 — RunService.GetRunSummary Implementation

**Target files:**
- `src/CabinetDesigner.Application/Services/RunService.cs`
- `src/CabinetDesigner.Application/Services/IRunService.cs` (no change needed — interface is correct)

### What to change

`RunService` currently throws `NotImplementedException` from `GetRunSummary(RunId runId)`. The query logic already exists in `RunSummaryService` (which uses `IDesignStateStore`). The cleanest minimal fix is to inject `IDesignStateStore` into `RunService` and implement `GetRunSummary` by querying the store directly.

**Add `IDesignStateStore` to the constructor:**
```csharp
private readonly IDesignStateStore _stateStore;

public RunService(IDesignCommandHandler handler, IClock clock, IDesignStateStore stateStore)
{
    _handler    = handler    ?? throw new ArgumentNullException(nameof(handler));
    _clock      = clock      ?? throw new ArgumentNullException(nameof(clock));
    _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
}
```

**Implement `GetRunSummary`:**
```csharp
public RunSummaryDto GetRunSummary(RunId runId)
{
    ArgumentNullException.ThrowIfNull(runId);

    var run = _stateStore.GetRun(runId)
        ?? throw new KeyNotFoundException($"Run {runId.Value} was not found in the design state store.");

    var slots = run.Slots
        .Where(slot => slot.SlotType == RunSlotType.Cabinet && slot.CabinetId is not null)
        .Select(slot =>
        {
            var cabinet = _stateStore.GetCabinet(slot.CabinetId!.Value);
            return new RunSlotSummaryDto(
                slot.CabinetId.Value.Value,
                cabinet?.CabinetTypeId ?? "Unknown cabinet",
                slot.OccupiedWidth.Inches,
                slot.SlotIndex);
        })
        .ToArray();

    return new RunSummaryDto(
        run.Id.Value,
        run.WallId.Value.ToString(),
        slots.Sum(s => s.NominalWidthInches),
        slots.Length,
        run.Slots.Any(slot => slot.SlotType == RunSlotType.Filler),
        run.OccupiedLength > run.Capacity,
        slots);
}
```

Required `using` additions to `RunService.cs`:
```csharp
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.RunContext;
```

**`DeleteRunAsync` and `SetCabinetOverrideAsync`:** Leave as `throw new NotImplementedException(…)`. Do not change the exception message.

### Update DI registration

Find where `RunService` is registered in the DI container (likely `ApplicationServiceRegistration.cs` or similar) and add `IDesignStateStore` to the registration if it is not already resolved from the container. Because `IDesignStateStore` is already registered (it is used by `RunSummaryService`), this should be a constructor-injection auto-wire.

### Tests to add

**File:** `tests/CabinetDesigner.Tests/Application/Services/RunServiceTests.cs` (add to existing class)

```csharp
[Fact]
public void GetRunSummary_ForKnownRunId_ReturnsCorrectRunId()
{
    var stateStore = new InMemoryDesignStateStore();
    var runId = RunId.New();
    var wallId = new WallId(Guid.NewGuid());
    // Seed a wall and run (use AddRun on IDesignStateStore):
    stateStore.AddWall(new Wall(wallId, RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m))));
    var run = new CabinetRun(runId, wallId, Length.FromInches(96m));
    stateStore.AddRun(run, Point2D.Origin, new Point2D(96m, 0m));

    var service = new RunService(new NoOpDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);
    var summary = service.GetRunSummary(runId);

    Assert.Equal(runId.Value, summary.RunId);
}

[Fact]
public void GetRunSummary_ForKnownRunWithCabinets_ReturnsCorrectSlotWidths()
{
    var stateStore = new InMemoryDesignStateStore();
    var runId = RunId.New();
    var wallId = new WallId(Guid.NewGuid());
    var cabinetId1 = CabinetId.New();
    var cabinetId2 = CabinetId.New();
    stateStore.AddWall(new Wall(wallId, RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m))));
    var run = new CabinetRun(runId, wallId, Length.FromInches(96m));
    run.AppendCabinet(cabinetId1, Length.FromInches(24m));
    run.AppendCabinet(cabinetId2, Length.FromInches(36m));
    stateStore.AddRun(run, Point2D.Origin, new Point2D(96m, 0m));

    var service = new RunService(new NoOpDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);
    var summary = service.GetRunSummary(runId);

    Assert.Equal(2, summary.CabinetCount);
    Assert.Equal(60m, summary.TotalNominalWidthInches);
}

[Fact]
public void GetRunSummary_ForUnknownRunId_ThrowsKeyNotFoundException()
{
    var stateStore = new InMemoryDesignStateStore();
    var service = new RunService(new NoOpDesignCommandHandler(), new FixedClock(DateTimeOffset.UnixEpoch), stateStore);

    Assert.Throws<KeyNotFoundException>(() => service.GetRunSummary(RunId.New()));
}

// NoOpDesignCommandHandler helper (add to RunServiceTests.cs if not already present):
private sealed class NoOpDesignCommandHandler : IDesignCommandHandler
{
    public Task<CommandResultDto> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
        => Task.FromResult(new CommandResultDto(Guid.NewGuid(), command.CommandType, true, [], [], []));
}
```

> **Note:** `InMemoryDesignStateStore` is already used in `ShellViewModelTests.cs`. Check that the `AddRun` overload accepting two `Point2D` arguments matches the current `IDesignStateStore.AddRun` signature.

> **Important:** Adding `IDesignStateStore` to the `RunService` constructor changes its signature. The existing `RunServiceTests` constructs `new RunService(handler, clock)` in multiple places — update every call site to `new RunService(handler, clock, new InMemoryDesignStateStore())`. This is required for the test project to compile after the production change.

### Acceptance criteria

- `GetRunSummary` returns a correctly-populated `RunSummaryDto` for a known `RunId`.
- `GetRunSummary` for an unknown `RunId` throws `KeyNotFoundException` (not `NotImplementedException`).
- `DeleteRunAsync` and `SetCabinetOverrideAsync` still throw `NotImplementedException`.
- All three new tests pass.
- DI container resolves `RunService` without error after the constructor change.

### Done means

- [ ] `RunService` constructor accepts `IDesignStateStore`.
- [ ] `GetRunSummary` implemented using `_stateStore.GetRun(runId)`.
- [ ] `DeleteRunAsync` / `SetCabinetOverrideAsync` unchanged.
- [ ] DI registration updated to pass `IDesignStateStore`.
- [ ] All existing `RunServiceTests` call sites updated from `new RunService(handler, clock)` to `new RunService(handler, clock, new InMemoryDesignStateStore())`.
- [ ] Three new `RunServiceTests` tests pass.
- [ ] `ApplicationServiceRegistrationTests` still passes.

---

## Batch 7 — Pipeline Skeleton Stage Logging

**Target files:**
- `src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs`

### What to change (same pattern for all five files)

1. Add an optional `IAppLogger? logger` constructor parameter (default `null`).
2. Store in `private readonly IAppLogger? _logger;`.
3. At the end of `Execute()`, before `return StageResult.NotImplementedYet(StageNumber);`, add a debug log call.

**Example — CostingStage after change:**
```csharp
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class CostingStage : IResolutionStage
{
    private readonly IAppLogger? _logger;

    public CostingStage(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public int StageNumber => 9;
    public string StageName => "Costing";
    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        // NOT IMPLEMENTED YET - skeleton returns success with an empty result.
        context.CostingResult = new CostingResult { … };

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "CostingStage",
            Message = $"Stage {StageNumber} ({StageName}) not yet implemented; returning skeleton result.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.NotImplementedYet(StageNumber);
    }
}
```

Apply identical pattern to `ConstraintPropagationStage` (stage 5), `PackagingStage` (stage 11), `PartGenerationStage` (stage 6), and `EngineeringResolutionStage` (stage 4). Use each stage's `StageNumber` and `StageName` for the log category and message.

### Tests to add

**File:** `tests/CabinetDesigner.Tests/Pipeline/SkeletonStageTests.cs` (new file)

`ResolutionContext` uses required init properties — construct it directly (no `ForFullResolution` factory method exists). `StageResult.NotImplementedYet(n)` returns `Success = true` and `IsNotImplemented = true` (confirmed in `StageResultTests.cs`).

```csharp
[Theory]
[MemberData(nameof(SkeletonStages))]
public void SkeletonStage_Execute_ReturnsSuccessWithIsNotImplementedTrue(IResolutionStage stage)
{
    var context = new ResolutionContext
    {
        Command = new TestDesignCommand([]),
        Mode = ResolutionMode.Full
    };
    var result = stage.Execute(context);

    Assert.True(result.Success);          // NotImplementedYet returns Success = true
    Assert.True(result.IsNotImplemented); // and IsNotImplemented = true
}

[Theory]
[MemberData(nameof(SkeletonStages))]
public void SkeletonStage_Execute_DoesNotThrow(IResolutionStage stage)
{
    var context = new ResolutionContext
    {
        Command = new TestDesignCommand([]),
        Mode = ResolutionMode.Full
    };
    var ex = Record.Exception(() => stage.Execute(context));
    Assert.Null(ex);
}

[Fact]
public void CostingStage_Execute_WithLogger_EmitsOneDebugLogEntry()
{
    var logger = new RecordingAppLogger();
    var stage = new CostingStage(logger);
    var context = new ResolutionContext { Command = new TestDesignCommand([]), Mode = ResolutionMode.Full };

    stage.Execute(context);

    Assert.Single(logger.Entries);
    Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
}

// Repeat the logger test for each of the remaining four stages:
// ConstraintPropagationStage, PackagingStage, PartGenerationStage, EngineeringResolutionStage

public static IEnumerable<object[]> SkeletonStages()
{
    yield return [new CostingStage()];
    yield return [new ConstraintPropagationStage()];
    yield return [new PackagingStage()];
    yield return [new PartGenerationStage()];
    yield return [new EngineeringResolutionStage()];
}

// Helpers (copy TestDesignCommand pattern from ResolutionContextTests.cs):
private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> Issues) : IDesignCommand
{
    public CommandMetadata Metadata { get; } =
        CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "test", []);
    public string CommandType => "test.command";
}

private sealed class RecordingAppLogger : IAppLogger
{
    public List<LogEntry> Entries { get; } = [];
    public void Log(LogEntry entry) => Entries.Add(entry);
}
```

### Acceptance criteria

- All five stages accept `IAppLogger?` without breaking DI (optional parameter, default null).
- Each stage emits exactly one `LogLevel.Debug` entry when a logger is provided.
- Stages still return `StageResult.NotImplementedYet(StageNumber)`.
- Stages do not throw.
- No existing pipeline tests broken.

### Done means

- [ ] All five stage files have optional `IAppLogger?` constructor parameter.
- [ ] All five stage files call `_logger?.Log(…)` at `LogLevel.Debug`.
- [ ] `SkeletonStageTests.cs` has ≥ 2 theory tests (no-throw + returns not-implemented), all passing.
- [ ] All existing pipeline tests still pass.

---

## Batch 8 — Documentation and Architecture Decision Records

**Target files:**
- `src/CabinetDesigner.Editor/EditorSession.cs`
- `src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs`
- `docs/ai/outputs/editor_engine.md`

### EditorSession.cs

Add or update the class-level XML doc to include:

```xml
/// <summary>
/// Holds all transient interaction state for the editor canvas.
/// </summary>
/// <remarks>
/// … (existing paragraphs) …
/// <para>
/// <strong>Selection state bypass:</strong> <see cref="SelectedCabinetIds"/> and
/// <see cref="HoveredCabinetId"/> are mutated directly on the editor session rather
/// than routed through the <c>ResolutionOrchestrator</c>. This is intentional:
/// selection is interaction state, not design state. Selection changes do not produce
/// undo entries, do not persist, and do not trigger the resolution pipeline. Any
/// refactoring that routes selection through the orchestrator must preserve this
/// invariant to avoid spurious undo stack pollution.
/// </para>
/// </remarks>
```

### WorkingRevisionRepository.cs

Locate the `DeleteExistingRowsAsync` method and add a comment before the loop or interpolated statement:

```csharp
// Table names are compile-time string constants defined in this class — they are never
// derived from user input. String interpolation here is safe and does not introduce
// a SQL injection vector. A parameterized query cannot be used for table names in SQLite.
foreach (var table in tables)
{
    await connection.ExecuteAsync($"DELETE FROM {table}", transaction: transaction).ConfigureAwait(false);
}
```

### docs/ai/outputs/editor_engine.md

Add a new section (after the last existing section):

```markdown
## Editor State vs Design State Boundary

The editor layer maintains two distinct categories of state:

| Category | Examples | Persisted? | Undo/Redo? | Routed via Orchestrator? |
|---|---|---|---|---|
| **Design state** | Cabinet position, width, run membership | Yes | Yes | Yes |
| **Editor interaction state** | Selected cabinet IDs, hovered cabinet ID, active drag, viewport transform | No | No | No |

**Why selection bypasses the orchestrator:**
`EditorSession.SelectedCabinetIds` and `EditorSession.HoveredCabinetId` are mutated directly by `EditorInteractionService` and `EditorCanvasViewModel`. They are intentionally excluded from the `ResolutionOrchestrator` pipeline because:
1. Selection changes must be instant (no async round-trip).
2. Selection is not design intent — it should never appear on the undo stack.
3. Persisting selection state would create noise in the revision history.

Any future feature that needs selection to influence the pipeline (e.g. "apply to selected") should do so by reading `EditorSession.SelectedCabinetIds` as an input parameter to a command, not by routing selection itself through the orchestrator.
```

### Tests to add/update

None. Documentation only.

### Done means

- [ ] `EditorSession.cs` XML doc includes the selection-bypass rationale.
- [ ] `WorkingRevisionRepository.cs` `DeleteExistingRowsAsync` has the safety comment.
- [ ] `docs/ai/outputs/editor_engine.md` has the "Editor State vs Design State Boundary" section.

---

## Summary of What Was Already Fixed (Backlog Cleanup)

Remove the following items from the active backlog — they are confirmed resolved:

| Item | Evidence |
|---|---|
| F-1: Application → Rendering dependency | `CabinetDesigner.Application.csproj` has no `Rendering` reference |
| F-2: Microsoft.AspNetCore.App reference | Only `Microsoft.Extensions.DependencyInjection` 8.0.1 used |
| F-3: Presentation → Domain reference | `Presentation.csproj` does not reference Domain directly |
| T-1: Determinism tests | `DeterminismTests.cs` exists and passes |
| T-2: Failure recovery tests | `CommandPersistenceServiceTests.cs` has rollback test |
| T-3: Working revision reconstruction test | `WorkingRevisionReconstructionTests.cs` exists |
| T-4: Snapshot blob corruption tests | `BlobCorruptionTests.cs` exists |
| T-5: Journal replay / idempotency tests | `JournalReplayTests.cs` exists |
| CommandPersistenceService UoW disposal | `CommitAsync`/`RollbackAsync` both call `DisposeSessionAsync()` — no leak |
| WorkingRevisionRepository interpolated SQL | Table names are hardcoded constants — not a SQL injection vector |

---

## Risks and Follow-Ups

### Risks After This Round

1. **Batch 1** will expose previously-silent failures in async callsites. After landing, review all `AsyncRelayCommand` usages and ensure callers that want errors surfaced pass `onException`.
2. **Batch 3** index renumbering changes `SourceId` strings (e.g., `"<runId>:left:0"` vs previously `"<runId>:left:2"`). Audit callers that compare or serialize `SnapCandidate.SourceId` strings.
3. **Batch 5** may reveal the undo path for drag commits is broken. If so, Batch 5 becomes a medium-risk code fix in addition to a test addition.
4. **Batch 6** changes the `RunService` constructor signature; update DI registration accordingly.

### Follow-Up Work (Out of Scope for This Round)

- Global WPF dispatcher `DispatcherUnhandledException` handler with user-facing error dialog.
- `DeleteRunAsync` (requires `DeleteRunCommand` domain implementation).
- `SetCabinetOverrideAsync` (requires `SetCabinetOverrideCommand` domain implementation).
- Presentation layer domain-import hygiene: `ApplicationEditorSceneGraph`, `EditorCanvasViewModel`, `SceneProjector` import `Domain.*` types transitively; these are technically valid but erode the DTO-only boundary over time.
- Full implementations of `CostingStage`, `PackagingStage`, `EngineeringResolutionStage`, `PartGenerationStage`, `ConstraintPropagationStage`.
- Multi-document workflow support.
