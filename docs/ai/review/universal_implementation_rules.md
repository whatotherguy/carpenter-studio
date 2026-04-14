# Universal Implementation Rules for Carpenter Studio

These rules apply to ALL fix/feature prompts given to Codex. Every implementation prompt MUST reference this file.

---

## Architecture Constraints

1. **All design mutations flow through `ResolutionOrchestrator`** — no direct domain mutation from UI or services
2. **No primitive dimensions** — use `Length`, `Offset`, `Angle`, `Point2D`, `Vector2D`, `Rect2D`, `Thickness`
3. **Domain layer is UI-independent** — no WPF references, no `System.Windows` imports
4. **Persistence models are separate from domain entities** — never expose DB row types to domain
5. **Approved snapshots are immutable** — once committed, no UPDATE/DELETE
6. **All behavior must be deterministic** — no randomness, no floating-point math for dimensions
7. **Commands represent intent, not execution** — commands carry parameters, orchestrator executes

## Code Style

1. **Nullable reference types are enabled globally** — never suppress with `!` without a comment explaining why
2. **TreatWarningsAsErrors is ON** — all code must compile warning-free
3. **ImplicitUsings is OFF** — all using statements must be explicit
4. **C# 12 / .NET 8** — use modern syntax but don't force it where readability suffers
5. **Constructor null guards** — all injected dependencies must use `?? throw new ArgumentNullException(nameof(param))`
6. **No `async void`** except for WPF event handlers — and those MUST have a top-level try/catch
7. **Never discard Tasks with `_ =`** — use `async void` wrapper with error handling, or `.ContinueWith` for fire-and-forget
8. **Never call `.GetAwaiter().GetResult()` or `.Result`** — make the method async instead

## Thread Safety

1. **All singletons must be thread-safe** — use `lock`, `Interlocked`, or concurrent collections
2. **UI property changes must happen on the dispatcher thread** — wrap `NotifyCanExecuteChanged` and `OnPropertyChanged` in `Dispatcher.InvokeAsync` when called from event bus handlers
3. **WPF mouse capture is shared state** — never assume stacked capture; guard against concurrent left+middle button interactions
4. **Event bus handlers may fire on any thread** — always marshal to UI thread before touching ViewModel state

## WPF / MVVM

1. **ViewModels talk to Application layer only** — never import Domain directly
2. **No business logic in code-behind** — code-behind is limited to framework glue (e.g., canvas hosting)
3. **IDisposable chains must be complete** — if a VM owns a disposable resource, it must dispose it; if a host is IDisposable, the VM must call Dispose on it
4. **Use `AutomationProperties.Name`** on all interactive controls — buttons, canvas hosts, list items
5. **Clamp user-controlled values to valid ranges** — never allow zero-width cabinets, negative dimensions, or degenerate geometry from drag operations

## Persistence / SQLite

1. **No string interpolation for SQL identifiers** — use parameterized queries; if identifiers must be dynamic, validate against a compile-time allowlist
2. **Wrap check-then-act patterns in transactions** — TOCTOU races are real with SQLite
3. **Explicit rollback before dispose** — don't rely on ADO.NET dispose-means-rollback semantics
4. **Add ORDER BY tiebreakers** — when ordering by timestamps, add a secondary sort on a unique column (e.g., `id`)
5. **Don't query the same data twice** — cache intermediate results in locals

## Testing

1. **Every fix must include a regression test** — at minimum a unit test proving the bug existed and is now fixed
2. **Integration tests must test real code paths** — not just raw SQL; test through `CommandPersistenceService`, `SqliteUnitOfWork`, etc.
3. **No time-based waits in tests** — use deterministic synchronization; if `Task.Delay` exists in a fixture, it's a code smell
4. **Test edge cases explicitly** — zero-width, over-capacity, concurrent operations, empty collections, null inputs

## Validation

1. **Guard clauses before object construction** — validate inputs before allocating objects or GUIDs
2. **Domain invariants belong in the domain** — not silently clamped in the persistence layer
3. **`ContextualIssues` must be populated or removed** — don't ship dead data structures

## Error Handling

1. **Log before re-throwing** — especially in command handlers and orchestrator paths
2. **Synchronous throws in async methods are silent killers** — any code before the first `await` in an async method can throw without the Task ever being observed
3. **`BeginBusy()` / `EndBusy()` must be balanced** — use try/finally; if `BeginBusy` can throw, call it inside the try block

---

## Key Reference Files

| File | Purpose |
|------|---------|
| `docs/ai/context/architecture_summary.md` | Architecture guardrails, six realities, pipeline stages |
| `docs/ai/context/code_phase_global_instructions.md` | Coding rules, execution modes, output format |
| `docs/ai/outputs/domain_model.md` | Domain entity reference |
| `docs/ai/outputs/application_layer.md` | Service and handler design |
| `docs/ai/outputs/editor_engine.md` | Editor interaction model |
| `docs/ai/outputs/persistence_layer_plan.md` | Persistence strategy |
| `docs/ai/outputs/validation_engine.md` | Validation framework |
| `docs/ai/review/evaluation_report.md` | Full bug/gap evaluation with line numbers |
