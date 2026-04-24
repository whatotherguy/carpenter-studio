# Carpenter Studio — Alpha Prep Codex Prompts (B, C, D, E)

Paste-and-run prompts covering the four alpha-testing blocker items that were not the presentation-test-suite flake (which was task A, already fixed on branch).

Target model: **Codex GPT-5.4-mini**. Each prompt is self-contained, names the files to read first, the exact changes to make, and the verification steps. Do not run them in parallel — later prompts assume the earlier ones landed.

---

## Conventions every prompt must follow

- **Stack**: WPF/.NET 8, MVVM, DI through `ApplicationServiceRegistration` / `PersistenceServiceRegistration`.
- **Layering** (strict, do not cross): `Domain` → `Application` → `Presentation` → `App`. `Rendering` consumes `Application`. `Persistence` implements `Application` interfaces.
- **Events**: cross-thread UI reactions funnel through `CabinetDesigner.Presentation.UiDispatchHelper.Run(...)`. Do NOT reintroduce an inline `DispatchIfNeeded` — the helper is the single chokepoint.
- **Logging**: use `IAppLogger.Log(new LogEntry { ... })` with category `Application`, `Presentation`, `Persistence`, or `App` as appropriate. Never `Console.WriteLine` / `Debug.WriteLine` in product code.
- **Commands**: user actions invoked from the shell go through `AsyncRelayCommand`; it already logs + publishes `CommandExecutionFailedEvent` on exception. Do not add parallel swallowing try/catches around it.
- **Tests**: xUnit. No mocks for the database — persistence tests use the real SQLite test fixtures. Run `dotnet test` from the repo root and confirm 0 failed. Presentation test collection is serialized via `xunit.runner.json` — do not change that.
- **No `mkdir`** / no deleting data. If a file path doesn't exist, use `Write`; if it does, use `Edit`.
- **Output rules**: report exactly what changed (files + one-line summary per file), the tests you added, and the `dotnet test` result. No celebratory prose.

---

## Execution order

| Order | Prompt | Purpose |
|---|---|---|
| 1 | **P-B** | Alpha-limitation guardrails + in-app messaging |
| 2 | **P-C** | Open / save / close / autosave recovery: end-to-end test coverage (manual-test replacement) |
| 3 | **P-D** | Exception surfacing + command-failure logging hardening |
| 4 | **P-E** | Interaction polish pass (focus, keyboard, status, empty states) |

Run P-B first because the guardrail metadata it introduces (the feature-flag/limitation surface) is referenced by P-E's empty-state copy.

---

## Prompt P-B — Alpha limitation guardrails and messaging

```text
Goal:
Surface the alpha-stage limitations in a way that prevents testers from hitting
silently-unsupported flows and produces actionable "we know about this" messaging
when they do.

Read first (in this order):
- docs/ai/alpha_prep/codex_prompts.md  (conventions section above)
- docs/ai/finish_round/final_finish_audit.md
- src/CabinetDesigner.Application/ApplicationServiceRegistration.cs
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/RoomsPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs
- src/CabinetDesigner.Application/Services/RunService.cs
- src/CabinetDesigner.Application/Services/CutListExportWorkflowService.cs
- src/CabinetDesigner.Application/Events/ApplicationEvents.cs
- any viewmodels whose commands throw NotImplementedException or silently no-op

Deliverables:

1. Introduce CabinetDesigner.Application.Diagnostics.AlphaLimitations (new file):
   - Static class with a single public method:
       IReadOnlyList<AlphaLimitation> All { get; }
     Each AlphaLimitation is a record: (string Code, string Title, string UserFacingMessage, AlphaArea Area).
     AlphaArea is an enum: Editor, Catalog, Properties, Export, Persistence, RunPlanning, General.
   - Populate from an inventory of currently-unsupported flows derived from:
       * RunService methods that throw NotImplementedException
       * Catalog entries that cannot be placed
       * Cabinet property edits that aren't persisted
       * Export formats that aren't implemented
     Each entry has a stable Code (e.g., "ALPHA-RUN-INSERT-NOT-IMPLEMENTED")
     and a 1-2 sentence user-facing message that does NOT blame the user.
   - This class lives in Application because it's the single source of truth; it
     does not reference Presentation.

2. Add CabinetDesigner.Application.Events.AlphaLimitationEncounteredEvent
   (record: AlphaLimitation Limitation, string? ContextHint) : IApplicationEvent.

3. For every currently-unsupported flow identified, replace the
   NotImplementedException / silent no-op / swallowed catch with:
       _eventBus.Publish(new AlphaLimitationEncounteredEvent(
           AlphaLimitations.AllByCode["ALPHA-…"],
           contextHint));
       return CommandResultDto.NoOp("…")  // or equivalent failure result
   Do NOT throw. The goal is that the user sees a non-fatal message and the app
   stays usable.

4. StatusBarViewModel: subscribe to AlphaLimitationEncounteredEvent and set
   StatusMessage to $"Not yet in alpha: {limitation.Title}. {limitation.UserFacingMessage}".
   Dispatch via UiDispatchHelper.Run. Do not add a new ribbon of UI — status bar is enough.

5. Add a new toolbar menu item "Alpha limitations" in MainWindow.xaml that opens a
   modal listing AlphaLimitations.All grouped by Area. Implement as
   AlphaLimitationsDialog.xaml + AlphaLimitationsDialogViewModel. View binds to
   the viewmodel; code-behind only sets DataContext. Close with OK.

6. Wire the registration in ApplicationServiceRegistration if AlphaLimitations
   needs DI (static is fine if it's pure data).

Tests to add:

- tests/CabinetDesigner.Tests/Application/Diagnostics/AlphaLimitationsTests.cs
  * Codes are unique.
  * Every AlphaArea is represented by at least one entry that actually fires
    (regression test: scan the product assemblies for NotImplementedException
    throws inside Application.Services and assert each throw was replaced).
- tests/CabinetDesigner.Tests/Presentation/StatusBarViewModelAlphaTests.cs
  * Publishing AlphaLimitationEncounteredEvent updates StatusMessage with the
    limitation Title and UserFacingMessage.
- tests/CabinetDesigner.Tests/Presentation/AlphaLimitationsDialogViewModelTests.cs
  * Groups by Area. Ordering is deterministic (Area, then Code).

Verification:

- `dotnet build -warnaserror` succeeds.
- `dotnet test` — 0 failed. Run twice in a row to confirm no presentation flake.
- Smoke: run the app, open the Alpha Limitations dialog, click at least one
  unsupported flow (e.g., attempt an action that was previously throwing), and
  confirm the status bar message appears and the app stays responsive.

Report:
- List of files added/changed with one-line summary each.
- List of NotImplementedException / silent-no-op sites replaced (path + method).
- dotnet test output line (Passed/Failed counts).
```

---

## Prompt P-C — Open / Save / Close / Autosave recovery test coverage

```text
Goal:
Replace the "manually test open/save/close/autosave" item with automated coverage
that proves every project lifecycle path — including a mid-session crash
simulation — works end-to-end against real SQLite persistence.

Read first:
- docs/ai/alpha_prep/codex_prompts.md
- docs/ai/outputs/persistence_strategy.md
- src/CabinetDesigner.Application/Services/ProjectService.cs
- src/CabinetDesigner.Application/Services/IProjectService.cs
- src/CabinetDesigner.Application/Persistence/IAutosaveCheckpointRepository.cs
- src/CabinetDesigner.Persistence/Repositories/AutosaveCheckpointRepository.cs
- src/CabinetDesigner.Persistence/UnitOfWork/CommandPersistenceService.cs
- src/CabinetDesigner.Persistence/Migrations/StartupOrchestrator.cs
- tests/CabinetDesigner.Tests/Application/Services/ProjectServiceTests.cs
- tests/CabinetDesigner.Persistence.Tests/Integration/PersistenceIntegrationTests.cs
- tests/CabinetDesigner.Persistence.Tests/Repositories/AutosaveCheckpointRepositoryTests.cs
- tests/CabinetDesigner.Persistence.Tests/Fixtures/TestData.cs

Deliverables:

1. tests/CabinetDesigner.Persistence.Tests/Integration/ProjectLifecycleIntegrationTests.cs (new)

   All tests run against a real temp-file SQLite DB via the existing
   SqliteTestFixture pattern. No mocks. Clean up the temp file in Dispose.

   Scenarios to cover, each as its own [Fact]:

   - CreateProject_ThenOpen_RoundTripsAllState
     Create a project, add a room, a wall, a cabinet run with one cabinet, save.
     Close the project. Reopen it. Assert the exact same domain state is visible
     (room count, wall coordinates, cabinet dimensions).

   - Save_IsIdempotent_AndStableHash
     Save the same in-memory state twice; the content hash on disk must be
     byte-identical (use the packaging-stage hash contract, or if not present,
     assert WorkingRevision row bytes are equal).

   - CloseWithUnsavedChanges_DropsUnsavedState
     Make a change after save, call CloseAsync without saving, reopen the same
     project — the unsaved change is gone, the last saved state is intact.

   - AutosaveCheckpoint_CreatedOnCommandSuccess
     Execute a design command, then query AutosaveCheckpointRepository for the
     project — a checkpoint row exists with the command's revisionId.

   - CrashRecovery_ReopensFromLatestCheckpoint
     Simulate a crash by: open project → make change (creates autosave) →
     do NOT call SaveAsync → dispose the ProjectService → create a fresh
     StartupOrchestrator against the same DB path → the orchestrator must
     detect the checkpoint and surface recoverable state.
     Assert: the recovered WorkingRevision matches the post-autosave state,
     not the pre-autosave state.

   - ConcurrentSaves_SerializedCorrectly
     Kick off two SaveAsync calls from separate tasks. Neither throws; final
     on-disk state is consistent (either both apply or the second is a no-op
     against the already-persisted state — whichever the current design
     specifies; assert the invariant, don't invent one).

2. tests/CabinetDesigner.Tests/Application/Services/ProjectServiceLifecycleTests.cs (new)

   Unit-level (recording repos, no SQLite) coverage of the orchestration:
   - OpenProjectAsync emits ProjectOpenedEvent exactly once.
   - CloseAsync emits ProjectClosedEvent exactly once.
   - CreateProjectAsync followed by OpenProjectAsync without a save does not
     lose the WorkingRevision (regression for Q-3 in the finish round).
   - SaveAsync failure (repo throws) propagates the exception; ProjectService
     does not swallow.

3. If the current ProjectService swallows exceptions on save failure, STOP and
   fix ProjectService to propagate, then write the test that proves it.

4. If the StartupOrchestrator does not currently expose a way to query
   "is there a crash-recovery checkpoint for project X", add the minimal
   public surface needed for the CrashRecovery_ReopensFromLatestCheckpoint test.
   Keep the new surface narrow — one method returning the latest AutosaveCheckpoint
   DTO or null.

Out of scope:
- Do not redesign autosave cadence.
- Do not touch the editor/canvas.
- Do not add UI prompts for crash recovery in this prompt — that's a later task.

Verification:
- `dotnet build -warnaserror` succeeds.
- `dotnet test` — 0 failed. Run three times consecutively; all three must pass.
- List every new test and its initial pass/fail before any production-code
  change (TDD discipline: write the test, see it fail, then fix).

Report:
- Each new test file + the scenarios it covers.
- Any production-code changes required to make the tests pass (with 1-line why).
- dotnet test counts.
```

---

## Prompt P-D — Exception surfacing and command-failure logging

```text
Goal:
Every exception that happens during a user action must (a) be logged at Error
with enough context to reproduce, and (b) be surfaced to the user via the
status bar — not swallowed, not silently rethrown into an empty catch.

Read first:
- docs/ai/alpha_prep/codex_prompts.md
- src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs
- src/CabinetDesigner.Application/Events/ApplicationEvents.cs
- src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs
- src/CabinetDesigner.Application/Diagnostics/IAppLogger.cs
- src/CabinetDesigner.Application/Diagnostics/TextFileAppLogger.cs
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/RoomsPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/ProjectStartupViewModel.cs
- src/CabinetDesigner.App/App.xaml.cs  (or wherever DispatcherUnhandledException is)
- tests/CabinetDesigner.Tests/Presentation/Commands/AsyncRelayCommandTests.cs

Deliverables:

1. Extend CommandExecutionFailedEvent to carry a stable correlation id and the
   command name (so log lines can be matched with user reports):
     CommandExecutionFailedEvent(string CommandName, string Message,
                                 Exception Exception, Guid CorrelationId)
   Default a new Guid at the publish site.

2. Update AsyncRelayCommand to:
   - Accept a `string commandName` constructor parameter (required; default
     "unnamed-command" is NOT acceptable — force callers to pass one).
   - Pass commandName + a new correlationId into both the LogEntry and the
     published event. LogEntry.Metadata (or equivalent) must include the
     correlationId.
   - Update every AsyncRelayCommand construction site in the viewmodels
     (ProjectStartupViewModel, ShellViewModel, RoomsPanelViewModel,
     PropertyInspectorViewModel, etc.) to pass a meaningful command name
     ("project.open", "project.create", "room.add", "cabinet.resize", etc.).

3. StatusBarViewModel: keep existing CommandExecutionFailedEvent subscription.
   Update the status message to include the correlationId suffix in a short form:
   $"Error in {commandName}: {message} (ref: {correlationId:N[..8]})".
   So a tester can copy/paste the ref and it maps to the log line.

4. App-level unhandled exception handler:
   In App.xaml.cs register Application.DispatcherUnhandledException and
   TaskScheduler.UnobservedTaskException. Both must:
   - Log at Error with the full exception (category "App").
   - Publish CommandExecutionFailedEvent with commandName = "app.unhandled"
     so the status bar shows something instead of a silent crash.
   - Set e.Handled = true only for DispatcherUnhandledException, and only after
     logging. Do not hide fatal errors — if the exception is a
     StackOverflowException / OutOfMemoryException / AccessViolationException,
     let it propagate.

5. Scan viewmodels for any `catch (NotImplementedException) { }` or
   `catch (Exception) { /* swallow */ }` patterns. Each must either:
   - Be removed (if the underlying path no longer throws after P-B), or
   - Be replaced with: log at Error + publish CommandExecutionFailedEvent.

Tests to add or extend:

- tests/CabinetDesigner.Tests/Presentation/Commands/AsyncRelayCommandTests.cs
  * Constructor requires a commandName.
  * Thrown exception publishes CommandExecutionFailedEvent with matching
    commandName and a non-empty correlationId.
  * Logger receives a LogEntry at Error level containing both commandName and
    the correlationId.
- tests/CabinetDesigner.Tests/Presentation/StatusBarViewModelErrorFormattingTests.cs (new)
  * Event with commandName "cabinet.resize", message "bad width" produces the
    exact StatusMessage format specified above.
- tests/CabinetDesigner.Tests/App/AppUnhandledExceptionTests.cs (new, if App
  is unit-testable; otherwise document as manual smoke).
  * Simulate DispatcherUnhandledException handler with a sample exception;
    assert the logger was called with category "App" and Level=Error.

Out of scope:
- Do not change the log format of TextFileAppLogger on disk.
- Do not add crash dialogs. Status bar is the surface for alpha.

Verification:
- `dotnet build -warnaserror` succeeds.
- `dotnet test` — 0 failed. Three consecutive runs.
- Manual smoke: trigger an exception by opening a nonexistent file path; the
  status bar shows the error with a ref:…; the log file contains a matching
  correlation id.

Report:
- Every AsyncRelayCommand construction site updated (file:line, command name).
- Every swallowed-catch site removed/replaced (file:line).
- dotnet test counts.
```

---

## Prompt P-E — Interaction polish pass

```text
Goal:
Eliminate the rough edges that would confuse an alpha tester but are below the
bar for filing a bug — focus behavior, keyboard shortcuts, empty states,
busy indicators, duplicate-click protection.

Run P-B and P-D first. P-E's empty-state copy references AlphaLimitations, and
its click-suppression test piggybacks on the AsyncRelayCommand changes from P-D.

Read first:
- docs/ai/alpha_prep/codex_prompts.md
- src/CabinetDesigner.App/MainWindow.xaml
- src/CabinetDesigner.Presentation/Views/*.xaml
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/RoomsPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/ProjectStartupViewModel.cs
- src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs
- src/CabinetDesigner.Application/Diagnostics/AlphaLimitations.cs  (from P-B)

Polish items (do all of them):

1. Startup focus.
   When the shell opens to the startup screen, focus must land on the
   "Create new project" input (or the recent-projects list if non-empty). Use
   FocusManager / Keyboard.Focus in XAML; do not put focus logic in the viewmodel.

2. Enter-key activation.
   In the startup view, Enter inside the project-name text box triggers
   CreateProjectCommand. In the "add room" input, Enter triggers AddRoomCommand.
   Use KeyBinding in XAML so it's declarative.

3. Global shortcuts on the editor shell (add as KeyBindings in MainWindow.xaml):
   - Ctrl+N → new project (shell menu command)
   - Ctrl+O → open project
   - Ctrl+S → save
   - Ctrl+Z → undo
   - Ctrl+Y / Ctrl+Shift+Z → redo
   - Delete → delete selected cabinet (if EditorCanvasViewModel has a
     DeleteSelectedCommand; if not, skip and note it in the report)
   - F1 → open Alpha Limitations dialog (from P-B)
   Wire each to the corresponding command on ShellViewModel or its child VMs.
   If a target command does not exist, stop and ask rather than inventing one.

4. Empty-state copy.
   Every panel with an EmptyStateText property (RunSummaryPanelViewModel,
   IssuePanelViewModel, RoomsPanelViewModel, etc.) gets two-sentence copy:
   sentence one = "why it's empty now", sentence two = "what to do next".
   Where an empty state is caused by an alpha limitation, the second sentence
   ends with "(press F1 for alpha notes)".

5. Busy indicators.
   Every panel with an IsBusy property: the view must dim the content and
   show a small text indicator ("Loading…" / "Saving…") when IsBusy is true.
   Use a DataTrigger in XAML, not code-behind.

6. Duplicate-click protection.
   AsyncRelayCommand already disables itself while executing (IsExecuting=true,
   CanExecute returns false). Audit every Button/MenuItem binding to a
   command and confirm IsEnabled is bound to CanExecute (the WPF default for
   ICommand already does this — this step is a grep confirmation, not code).
   Report anything that uses Click= instead of Command= so a follow-up can
   convert it.

7. Selection feedback.
   When the user selects a cabinet on the canvas, the PropertyInspectorView must
   scroll the selected cabinet's section into view if it's off-screen. Use
   FrameworkElement.BringIntoView triggered from a viewmodel property change
   via an attached behavior (no code-behind).

8. Status bar truncation.
   Long status messages currently overflow. Set TextTrimming=CharacterEllipsis
   on the StatusMessage TextBlock and surface the full text in ToolTip.

Tests to add:

- tests/CabinetDesigner.Tests/Presentation/EmptyStateCopyTests.cs
  * Every panel viewmodel's EmptyStateText is non-empty, contains two sentences,
    and mentions F1 when the empty state is alpha-limitation-driven.
- tests/CabinetDesigner.Tests/Presentation/RunSummaryPanelViewModelTests.cs
  (extend) — no-runs and no-project cases match the new two-sentence format.
- Leave keybindings out of automated tests (WPF input routing is painful to
  test headlessly). Add a short `docs/ai/alpha_prep/manual_polish_checklist.md`
  listing the keybindings so the tester can verify each in one session.

Out of scope:
- No icon changes, no theming, no color tweaks.
- No new panels or windows beyond Alpha Limitations (which P-B already added).
- Do not change dispatcher behavior — UiDispatchHelper.Run stays the sole path.

Verification:
- `dotnet build -warnaserror` succeeds.
- `dotnet test` — 0 failed. Two consecutive runs.
- Manual smoke: launch app, run through the keybindings once, confirm each
  produces the expected effect. Note any that did not work in the report and
  leave the code landing the binding in place (so the follow-up is obvious).

Report:
- Polish items 1–8: done / partial / skipped with reason.
- Every XAML file touched + one-line reason.
- dotnet test counts.
- manual_polish_checklist.md path.
```

---

## Why only four prompts

Task A (presentation-test-suite flake) was handled directly on branch — see the
`UiDispatchHelper` + the per-viewmodel `DispatchIfNeeded` removal. It is not in
this pack because it's already in `master`. Tasks B–E are the items that read
better as scoped, single-pass Codex runs.

## When to stop and ask

If a prompt's "Deliverables" section names a file or method that does not
exist at the path given, stop and report. Do NOT invent a new public surface to
make the instruction fit — the prompt was written against a specific snapshot
and either the snapshot or the instruction is wrong. Either is easy to fix once
surfaced; inventing silently is not.
