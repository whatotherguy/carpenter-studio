# P10 — Cross-Cutting Technical Concerns

Source: `cabinet_ai_prompt_pack_v4_1_full.md`
Context: `architecture_summary.md`, `commands.md`, `orchestrator.md`, `why_engine.md`, `application_layer.md`, `validation_engine.md`, `persistence_strategy.md`

---

## 1. Goals

- Define the shared technical concerns that span all projects in the solution without duplicating subsystem responsibilities
- Establish logging, diagnostics, and telemetry boundaries for a local desktop application
- Codify the error/result handling strategy so every layer speaks the same failure language
- Define configuration and settings layering that respects the parameter hierarchy
- Specify eventing contracts for intra-process communication
- Set caching and performance budgets appropriate to interactive design on commodity hardware
- Formalize determinism guardrails so the resolution pipeline is always reproducible
- Define crash recovery and supportability expectations
- Establish a testing taxonomy that covers the full system from unit through snapshot tests

---

## 2. Scope

### In Scope

| Concern | Description |
|---|---|
| Logging and diagnostics | Structured logging contracts, log levels, what to log, what not to log |
| Error/result model | `Result<T>` patterns, exception policy, failure propagation |
| Configuration and settings | Shop standards, project defaults, user preferences, runtime settings |
| Eventing | `IApplicationEventBus` delivery semantics, event taxonomy, subscriber rules |
| Caching and performance | Cache lifetimes, invalidation strategy, performance budgets |
| Determinism | Rules ensuring identical inputs produce identical outputs across runs |
| Crash recovery and supportability | Autosave integration, diagnostic bundle, support workflow |
| Testing taxonomy | Unit, integration, property-based, snapshot, regression test categories |
| Security and privacy | Local-first data model, no telemetry home, file-level trust |
| Concurrency | Thread model, async policy, single-threaded pipeline guarantee |

### Out of Scope

| Concern | Owned By |
|---|---|
| WPF visual design (styles, themes, layout) | `CabinetDesigner.Presentation` |
| Domain logic (filler math, run resolution, assembly rules) | `CabinetDesigner.Domain` |
| Detailed persistence schema (tables, indexes, migration SQL) | `CabinetDesigner.Persistence` / `persistence_strategy.md` |
| Hardware catalog import, vendor-specific adapters | `CabinetDesigner.Integrations` |

---

## 3. Logging and Diagnostics

### 3.1 Logging Contract

All logging flows through a single abstraction. No subsystem writes directly to a file, console, or debug output.

```csharp
namespace CabinetDesigner.Infrastructure.Diagnostics;

/// <summary>
/// Application-wide logging abstraction.
/// Implementations: file sink (default), debug sink (dev builds).
/// No remote/cloud sinks — this is a local desktop application.
/// </summary>
public interface IAppLogger
{
    void Log(LogEntry entry);
}

public sealed record LogEntry
{
    public required LogLevel Level { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? CommandId { get; init; }
    public string? StageNumber { get; init; }
    public IReadOnlyDictionary<string, string>? Properties { get; init; }
    public Exception? Exception { get; init; }
}

public enum LogLevel
{
    Trace,      // Internal plumbing — off by default
    Debug,      // Developer-useful, off in release
    Info,       // Normal operational events
    Warning,    // Degraded but recoverable
    Error,      // Operation failed, user may notice
    Fatal       // Application cannot continue
}
```

### 3.2 Logging Rules

| Rule | Rationale |
|---|---|
| **Log at boundaries, not inside tight loops** | Geometry math, part iteration, and snap evaluation are hot paths. Logging inside them destroys performance. Log at stage entry/exit, command entry/exit, and service calls |
| **Structured properties, not string interpolation** | Every log entry carries a `Category` and optional `Properties` dictionary. Enables filtering and search in log files without regex |
| **Command ID flows through all logs during pipeline execution** | Every log entry produced during a command's lifecycle carries the `CommandId`. Enables "show me everything that happened for this command" queries |
| **No PII in logs** | File paths are local and acceptable. No user names, license keys, or customer project content in log entries |
| **No log-level-dependent behavior** | Code must not branch on whether logging is enabled. Logging is observation, not control flow |
| **Errors always include exception and context** | `LogLevel.Error` and `LogLevel.Fatal` entries must include the `Exception` property if one exists, and enough `Properties` to reproduce the failure |

### 3.3 Log Categories

| Category | Examples |
|---|---|
| `Pipeline` | Stage entry/exit, stage duration, command accepted/rejected |
| `Persistence` | Transaction begin/commit/rollback, migration applied, snapshot written |
| `Validation` | Rule execution count, blocking issue detected, validation run duration |
| `Editor` | Mode change, snap evaluation summary (not per-candidate), selection change |
| `Application` | Project open/close, service call, event published |
| `Infrastructure` | Settings loaded, cache hit/miss summary, autosave checkpoint |

### 3.4 Log Sink Strategy

- **Default sink:** Rolling file in `%LOCALAPPDATA%\CarpenterStudio\logs\`. One file per day, max 30 days retained. Plain text with structured prefix: `[timestamp] [level] [category] message {properties}`.
- **Debug sink (dev builds only):** `System.Diagnostics.Debug.WriteLine` mirror. Enabled via build configuration, never in release.
- **No remote telemetry.** This is a local desktop tool for carpenters. No phone-home, no crash reporting service, no analytics SDK. Diagnostic data stays on the user's machine.

### 3.5 Diagnostics Bundle

When a user reports a problem, the application can export a **diagnostic bundle** — a zip file containing:

- Last 7 days of log files
- Current settings (sanitized — no file paths that reveal directory structure beyond the app)
- Schema migration version
- .NET runtime version and OS version
- Last 50 command journal entries (serialized, no design content — command types and timestamps only)

The user initiates this manually from a Help menu. It is never automatic.

---

## 4. Error/Result Handling

### 4.1 Result Model

The system uses explicit result types instead of exceptions for expected failures. Exceptions are reserved for programming errors and unrecoverable infrastructure failures.

```csharp
namespace CabinetDesigner.Domain;

/// <summary>
/// Discriminated result type. Every operation that can fail
/// returns Result<T> instead of throwing.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly ResultError? _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result.");

    public ResultError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(ResultError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(ResultError error) => new(error);
    public static Result<T> Failure(string code, string message) =>
        new(new ResultError(code, message));
}

public sealed record ResultError(string Code, string Message)
{
    public IReadOnlyList<string>? AffectedEntityIds { get; init; }
    public Exception? InnerException { get; init; }
}
```

### 4.2 Exception Policy

| Category | Policy |
|---|---|
| **Domain invariant violations** | Throw `InvalidOperationException` or `ArgumentException`. These are programming errors — the caller violated a contract. Not caught at domain boundaries |
| **Expected business failures** | Return `Result<T>.Failure(...)` or `CommandResult.Failed(...)`. Never throw. Examples: validation rejection, entity not found, slot does not fit |
| **Infrastructure failures** | Catch at the infrastructure boundary (persistence, file I/O), wrap in `Result<T>.Failure(...)` with the original exception attached, and propagate up. Log at `Error` level |
| **Unrecoverable failures** | Let the exception propagate to the top-level handler in `CabinetDesigner.App`. Log at `Fatal` level. Show a crash dialog. Trigger autosave if possible |
| **Never catch `Exception` broadly in domain or application code** | Broad catches mask bugs. Only the top-level crash handler and infrastructure adapters may catch `Exception` |

### 4.3 Failure Propagation Path

```
Domain → throws on invariant violation (programming error)
       → returns Result<T>.Failure on business failure

Pipeline Stage → returns StageResult with issues on validation failure
              → context.HasBlockingIssues halts pipeline

Orchestrator → returns CommandResult.Failed with issues
            → never throws for business-logic failures

Application Service → returns CommandResultDto to ViewModel
                    → publishes DesignChangedEvent only on success

ViewModel → displays issues in UI
          → never catches and swallows silently
```

### 4.4 Non-Result (Unit) Operations

For operations that succeed or fail with no return value, use `Result<Unit>`:

```csharp
namespace CabinetDesigner.Domain;

public readonly struct Unit
{
    public static readonly Unit Value = default;
}
```

---

## 5. Configuration and Settings

### 5.1 Settings Hierarchy

Settings follow the same parameter hierarchy as design resolution (architecture_summary.md §Parameter Hierarchy), but at the application level:

```
1. Built-in defaults (compiled into the application — never missing)
2. Shop standards (shop_standards.json in %LOCALAPPDATA%\CarpenterStudio\)
3. User preferences (user_preferences.json — UI layout, display units, recent files)
4. Project-level settings (stored in the .cabinet file — project defaults, room defaults)
5. Runtime overrides (command-line arguments for testing/diagnostics)
```

Lower levels override higher levels. Every setting has a compiled default so the application can always start, even with no settings files present.

### 5.2 Settings Contract

```csharp
namespace CabinetDesigner.Infrastructure.Configuration;

/// <summary>
/// Reads resolved settings. Resolution order: built-in → shop → user → project → runtime.
/// Immutable snapshot — changes require calling Reload().
/// </summary>
public interface ISettingsProvider
{
    /// <summary>Get a setting value by key. Returns the resolved value from the highest-priority source.</summary>
    T Get<T>(string key);

    /// <summary>Get a setting value, returning the default if not explicitly configured.</summary>
    T GetOrDefault<T>(string key, T defaultValue);

    /// <summary>
    /// Reload all settings from disk. Called on project open, shop standards change,
    /// or user preferences save. Thread-safe — replaces the snapshot atomically.
    /// </summary>
    void Reload();

    /// <summary>The source that provided a given key's value (for diagnostics / "why is this set?").</summary>
    SettingsSource GetSource(string key);
}

public enum SettingsSource
{
    BuiltIn,
    ShopStandards,
    UserPreferences,
    Project,
    RuntimeOverride
}
```

### 5.3 Settings Categories

| Category | Storage | Examples |
|---|---|---|
| **Shop standards** | `shop_standards.json` | Default material, default overlay, standard cabinet depths, reveal width, default hardware line |
| **User preferences** | `user_preferences.json` | Display units (imperial/metric), autosave interval, snap grid visibility, recent file list, UI layout state |
| **Project settings** | Inside `.cabinet` file (SQLite) | Project name, default room height, default run height, cost markup percentage |
| **Runtime overrides** | Command-line or environment | Log level, diagnostic mode, WAL checkpoint interval |

### 5.4 Settings Rules

- **Settings files are JSON.** Human-readable, editable in a text editor for power users.
- **No settings file is required for startup.** Built-in defaults cover everything.
- **Shop standards are shared across projects.** Changing shop standards does not retroactively change existing projects — projects capture their effective settings at creation time.
- **Settings are read at startup and on explicit reload.** No file watchers — settings changes require a menu action or project open/close to take effect.
- **No encrypted settings.** This is a local desktop app with no secrets to protect at rest. License keys (if any, future) would use the Windows credential store, not a settings file.

---

## 6. Eventing

### 6.1 Application Event Bus

The `IApplicationEventBus` (defined in `application_layer.md` §6.4) is the sole intra-process eventing mechanism. All cross-layer communication after a command commit flows through events, not direct method calls from domain to presentation.

### 6.2 Delivery Semantics (Restated for Cross-Cutting Reference)

| Property | Guarantee |
|---|---|
| **Delivery order** | Synchronous, in subscription order |
| **Thread** | Caller's thread (UI thread for all command-originated events) |
| **Error isolation** | Handler exceptions are caught, logged, and swallowed. A broken subscriber does not abort the event chain |
| **Replay** | None. Events are fire-and-forget. Late subscribers read current state from services |
| **Queuing** | None. No background dispatch, no buffering |

### 6.3 Event Taxonomy

| Event | Published After | Consumers |
|---|---|---|
| `DesignChangedEvent` | Successful deep-path command execution | Canvas ViewModel, property panel, run summary panel |
| `ProjectOpenedEvent` | Project loaded from disk | Shell ViewModel, recent files, title bar |
| `ProjectClosedEvent` | Project closed | Shell ViewModel, editor cleanup |
| `RevisionApprovedEvent` | Revision frozen as approved snapshot | Revision history panel, export availability |
| `UndoAppliedEvent` | Successful undo | Canvas ViewModel, property panel |
| `RedoAppliedEvent` | Successful redo | Canvas ViewModel, property panel |
| `ValidationIssuesChangedEvent` | Validation run completes (Stage 10) | Status bar, issue panel |
| `SettingsChangedEvent` | Settings reloaded | Any ViewModel that displays setting-dependent values |
| `AutosaveCompletedEvent` | Autosave checkpoint written | Status bar (shows "saved" indicator) |

### 6.4 Eventing Rules

- **No event carries mutable domain state.** Events carry DTOs or immutable records only.
- **No event handler mutates domain state.** Handlers update ViewModel properties, refresh queries, or trigger UI updates. Never dispatch a new design command from inside an event handler.
- **Unsubscribe on dispose.** ViewModels must unsubscribe from events when they are deactivated or disposed. Leaked subscriptions cause stale updates and memory leaks.
- **No cascading events.** An event handler must not publish another event. If a workflow requires multi-step notification, the originating service publishes all events in sequence.

---

## 7. Caching and Performance

### 7.1 Performance Budgets

These budgets target commodity hardware: a 4-core Intel i5 (2020-era), 16 GB RAM, integrated GPU, SSD storage.

| Operation | Budget | Rationale |
|---|---|---|
| **Drag-time preview (fast path, stages 1–3)** | < 16 ms (60 fps) | Must feel instant during drag. Any frame drop is noticeable |
| **Command commit (deep path, all 11 stages)** | < 200 ms for single-cabinet operations | User drops a cabinet and sees final result. Above 200 ms feels sluggish |
| **Full project validation (Stage 10, 50-cabinet project)** | < 500 ms | Post-commit validation must not block the next interaction noticeably |
| **Project open (load from SQLite + hydrate domain)** | < 2 seconds for a 100-cabinet project | First meaningful paint. User should see the canvas within 2 seconds |
| **Snapshot approval (serialize + write blobs)** | < 3 seconds for a 100-cabinet project | Approval is a deliberate action — slightly longer is acceptable |
| **Autosave checkpoint** | < 50 ms | Must not cause UI jank. Runs as part of the command commit transaction |

### 7.2 Caching Strategy

Caching is minimal and explicit. The system favors recomputation from authoritative state over stale cached data.

| Cache | Scope | Lifetime | Invalidation |
|---|---|---|---|
| **Resolved parameter values** | Per-cabinet, per-run | Until next command commit affecting that entity | Invalidated by `DesignChangedEvent` scoped to affected entity IDs |
| **Snap candidate cache** | Editor, per drag operation | Duration of one drag gesture | Cleared on drag start and drag end |
| **Validation issue index** | Per-revision | Until next validation run | Replaced wholesale (see persistence_strategy.md §11.2) |
| **Material catalog lookup** | Application lifetime | Until catalog reload | Cleared on catalog import |
| **Rendered geometry cache** | Rendering layer | Until next `DesignChangedEvent` | Invalidated per affected entity |

### 7.3 Caching Rules

- **No implicit caches.** Every cache is a named, typed object with explicit lifetime and invalidation. No `Dictionary<string, object>` bags.
- **Cache misses are never errors.** A cache miss triggers recomputation. The system must produce correct results with all caches empty.
- **No cross-command cache assumptions.** A cache populated during command N must not be assumed valid during command N+1. The `DesignChangedEvent` with `AffectedEntityIds` is the invalidation signal.
- **Fast path must not populate deep-path caches.** Preview computations are approximate. They must not pollute caches that the deep path relies on for correctness.

---

## 8. Determinism Guardrails

Determinism is a non-negotiable architectural guardrail: same command + same state = same result. Every subsystem must obey these rules.

### 8.1 Rules

| Rule | Enforcement |
|---|---|
| **No `DateTime.Now` or `DateTimeOffset.UtcNow` in domain or application logic** | All timestamps come from `IClock` injected at the application layer. `IClock` is the single source of time. Tests inject a fixed clock |
| **No `Guid.NewGuid()` in domain logic** | Entity IDs are created via typed ID factories (`CabinetId.New()`) which call `Guid.NewGuid()`. In tests, these can be replaced with a deterministic ID generator for reproducibility |
| **No `Random` or non-deterministic ordering** | If ordering matters, sort explicitly. LINQ `.ToDictionary()`, `.ToHashSet()`, and `foreach` over `Dictionary<K,V>` have non-deterministic order. Use `SortedDictionary` or explicit `.OrderBy()` where order affects output |
| **No floating-point arithmetic for dimensions** | All dimensional math uses `decimal`-backed `Length` values. IEEE 754 `double` is banned for any value that affects cut lists, placement, or cost. The geometry foundation enforces this |
| **No culture-dependent formatting in logic** | `ToString()`, `Parse()`, and string comparisons in domain and persistence use `CultureInfo.InvariantCulture`. Display formatting (user-facing strings) uses the user's culture in the presentation layer only |
| **No environment-dependent behavior in domain** | Domain logic must not read environment variables, file system state, or registry keys. All external inputs enter through the application layer |
| **Pipeline stages execute in fixed order** | The orchestrator runs stages 1–11 in sequence. No stage reordering, no conditional skipping. A stage may no-op, but it always runs |
| **Explanation nodes are produced in execution order** | The Why Engine appends nodes as decisions happen. No post-hoc reordering. Order is part of the audit trail |

### 8.2 IClock Contract

```csharp
namespace CabinetDesigner.Application;

/// <summary>
/// Single source of time for the entire application.
/// Domain and application layers never call DateTimeOffset.UtcNow directly.
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

/// <summary>Production implementation. Registered as singleton.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

/// <summary>Test implementation. Advances manually.</summary>
public sealed class TestClock : IClock
{
    private DateTimeOffset _now;
    public DateTimeOffset Now => _now;

    public TestClock(DateTimeOffset startTime) => _now = startTime;
    public void Advance(TimeSpan duration) => _now += duration;
    public void Set(DateTimeOffset time) => _now = time;
}
```

### 8.3 Determinism Testing

Every pipeline stage and every domain operation that produces output from input must have at least one **determinism test**: run the same operation twice with identical inputs and assert identical outputs (including ordering). These tests are categorized as property-based tests in the test taxonomy.

---

## 9. Concurrency Model

### 9.1 Thread Model

The application is single-user, single-window, single-project-at-a-time. The threading model is deliberately simple:

| Thread | Owns | Notes |
|---|---|---|
| **UI thread (WPF Dispatcher)** | All ViewModel property changes, event handler execution, rendering | Standard WPF STA thread |
| **Pipeline execution** | Synchronous on the calling thread (UI thread via service call) | No background thread. No `Task.Run` for pipeline stages. Pipeline must complete within performance budgets on the UI thread |
| **Persistence I/O** | `async` methods with `ConfigureAwait(false)` inside persistence layer | SQLite calls are I/O-bound. `async` prevents blocking the UI thread during project open/save. Command commit transactions are fast enough to run synchronously (see §7.1) |
| **Autosave timer** | `DispatcherTimer` on UI thread | Fires on the UI thread. Persistence calls from the timer use `async` |

### 9.2 Async Policy

| Layer | Async Allowed | Rationale |
|---|---|---|
| **Domain** | No | Domain logic is pure computation. No I/O, no awaits, no `Task<T>` returns |
| **Application (pipeline execution)** | No | Pipeline stages are synchronous. `ResolutionOrchestrator.Execute()` returns `CommandResult`, not `Task<CommandResult>`. Keeps determinism simple and avoids hidden concurrency |
| **Application (services)** | Yes, for I/O-bound operations | `IProjectService.OpenProjectAsync()`, `SaveAsync()` — these hit SQLite. `async` prevents UI thread blocking |
| **Persistence** | Yes | All repository methods return `Task<T>`. Implementation uses async SQLite APIs |
| **Presentation** | Indirectly, via `async void` command handlers in ViewModels | WPF `ICommand.Execute` is `void`. ViewModels use `async void` for command handlers that call async services. Exceptions in `async void` are caught by the ViewModel's error handler, not swallowed |

### 9.3 Concurrency Rules

- **No concurrent command execution.** The orchestrator processes one command at a time. If a second command arrives while one is executing (should not happen in normal operation), it is queued or rejected — never processed concurrently.
- **No `lock` statements in domain or application code.** The single-threaded pipeline guarantee eliminates the need for locks. If a lock appears in domain code, it signals an architecture violation.
- **No `Parallel.For` or `Task.WhenAll` in the pipeline.** Even if individual stages could theoretically parallelize, the complexity and non-determinism risk outweigh the benefit for a desktop app.
- **SQLite access is serialized.** One connection per project, WAL mode, busy timeout of 0 (fail immediately if another process has the file open).

---

## 10. Crash Recovery and Supportability

### 10.1 Crash Recovery (Restated from persistence_strategy.md for Cross-Cutting Reference)

The recovery model is simple: every committed command writes to SQLite atomically. SQLite WAL mode guarantees that the on-disk state is always consistent with the last successfully committed command.

| Scenario | Recovery |
|---|---|
| **Application crash mid-session** | On next open, WAL auto-recovers. Working state is the last committed command. Undo stack is empty (in-memory only). User is notified of unclean shutdown |
| **Crash during command commit** | SQLite transaction rolls back. Working state is the state before the failed command. No partial writes |
| **Crash during snapshot approval** | Transaction rolls back. Revision remains in `under_review` state. User re-triggers approval |
| **Power loss** | WAL + SQLite's atomic commit guarantee. Same as application crash — last committed state is intact |

### 10.2 Unclean Shutdown Detection

On startup, the application checks `autosave_checkpoints.is_clean`. If `0`:
1. Log a warning: `"Previous session did not shut down cleanly"`
2. Display a non-modal notification to the user (not a blocking dialog)
3. Open the project normally — the working state is valid

### 10.3 Supportability

| Feature | Description |
|---|---|
| **Diagnostic bundle export** | See §3.5. User-initiated zip export of logs, settings, schema version, and command journal summary |
| **Schema version display** | Settings/About dialog shows current working schema version and snapshot schema versions present in the file |
| **Validation issue export** | Export current validation issues as a CSV or JSON for support communication |
| **Command journal viewer** | Dev/diagnostic mode only: a read-only view of the command journal showing command types, timestamps, and success/failure. No design content exposed |
| **"Last 10 commands" in crash dialog** | If the application crashes, the crash dialog shows the last 10 command types and timestamps from the journal. Helps pinpoint what triggered the crash |

---

## 11. Security and Privacy

### 11.1 Threat Model

This is a **local desktop application** with no network connectivity required for core functionality. The threat model is minimal:

| Threat | Assessment | Mitigation |
|---|---|---|
| **Unauthorized file access** | Low — relies on OS file permissions | No application-level encryption of `.cabinet` files. Users manage file access via Windows permissions and network shares |
| **Malicious project file** | Medium — a crafted `.cabinet` file could contain SQL injection in JSON blobs | All SQLite access uses parameterized queries. JSON deserialization uses strict schemas with no polymorphic type resolution from untrusted data |
| **Data loss** | High impact — a lost project represents hours of design work | Mitigated by autosave, WAL mode, and atomic transactions. Users should also maintain their own backups |
| **Privacy / PII exposure** | Low — project files contain design data, not personal information | No telemetry, no remote logging, no analytics. Diagnostic bundles are user-initiated and contain no design content |

### 11.2 Security Rules

- **All SQLite queries use parameterized statements.** No string concatenation for SQL. No exceptions.
- **JSON deserialization uses explicit type mappings.** No `TypeNameHandling.Auto` or equivalent. Snapshot blob deserialization uses versioned deserializers registered at startup, not reflection-based type resolution from the blob content.
- **No network calls from core application code.** Hardware catalog import (future) is an explicit user-initiated action in the Integrations layer, not an ambient background fetch.
- **No elevated permissions required.** The application runs as a standard user. All data is stored in `%LOCALAPPDATA%` or user-chosen directories.
- **File paths are validated before use.** Path traversal attacks via project file metadata are prevented by canonicalizing and validating all file paths at the infrastructure boundary.

---

## 12. Testing Taxonomy

### 12.1 Test Categories

All tests live in `CabinetDesigner.Tests`. Test classes are organized by category and subsystem.

| Category | Scope | Speed | Dependencies | Examples |
|---|---|---|---|---|
| **Unit** | Single class or method in isolation | < 10 ms each | None (no I/O, no database, no file system) | Geometry value object arithmetic, command structural validation, domain invariant enforcement, result model construction |
| **Integration** | Multiple components collaborating, including real infrastructure | < 500 ms each | Real SQLite in-memory database, real file system (temp directory) | Full command commit transaction, repository round-trips, mapper domain→row→domain, migration application |
| **Pipeline** | Full or partial resolution pipeline execution | < 1 second each | In-memory domain state, mock or real orchestrator | Command → orchestrator → all stages → result. Verifies stage ordering, delta capture, explanation node creation |
| **Property-Based** | Invariant verification across randomized inputs | < 50 ms per case, 100+ cases | None | Geometry operations are commutative/associative where expected, Length round-trips through serialization without drift, determinism (same input → same output) |
| **Snapshot** | Serialized output stability | < 100 ms each | Snapshot files on disk (checked into source control) | Approved snapshot blob deserialization across schema versions, command serialization format stability, DTO shape stability |
| **Regression** | Specific bug reproduction | Varies | As needed for the scenario | Named after the bug/issue that motivated them. Guard against recurrence |

### 12.2 Test Naming Convention

```
[SubjectUnderTest]_[Scenario]_[ExpectedOutcome]
```

Examples:
- `Length_FromInches_NegativeValue_ThrowsArgumentException`
- `AddCabinetToRunCommand_ValidateStructure_ZeroWidth_ReturnsError`
- `ResolutionOrchestrator_Execute_ValidCommand_ProducesDeltas`
- `CabinetRowMapper_RoundTrip_PreservesAllFields`
- `SnapshotDeserializerV2_DeserializeV2Blob_ReturnsValidSnapshot`

### 12.3 Test Infrastructure

| Component | Purpose |
|---|---|
| `TestClock` | Fixed-time clock for deterministic timestamps (see §8.2) |
| `TestIdGenerator` | Deterministic GUID sequence for reproducible entity IDs |
| `InMemorySqliteFixture` | Creates an in-memory SQLite database with all migrations applied. Disposed after each test class |
| `TestEventBus` | Captures published events for assertion. No real subscribers |
| `PipelineTestHarness` | Pre-configures a `ResolutionContext` with test data and runs selected stages |
| `SnapshotApprover` | Manages snapshot files: first run creates the snapshot, subsequent runs compare against it |

### 12.4 Testing Rules

- **Domain tests have zero infrastructure dependencies.** If a domain test needs a database, it belongs in the integration category.
- **Pipeline tests verify stage ordering and output shape.** They do not test individual domain rules — that is the unit test's job.
- **Property-based tests focus on algebraic properties.** Commutativity, associativity, round-trip stability, idempotency — not business scenarios.
- **Snapshot tests are append-only.** Adding a new snapshot test for a new version is expected. Deleting or modifying an existing snapshot file requires explicit justification (schema intentionally changed).
- **Regression tests cite the bug they guard against** in a comment or test attribute. Orphan regression tests (no citation) are cleaned up periodically.
- **No test may depend on execution order.** Tests run in parallel by default. Shared state between tests is forbidden.
- **xUnit is the test framework** unless the repository has already standardized on NUnit.

---

## 13. Invariants

1. **Every design state change flows through `ResolutionOrchestrator`.** No subsystem may mutate design state outside the pipeline. This is the architectural choke point.

2. **Every operation that can fail returns `Result<T>` or `CommandResult`.** Exceptions are reserved for programming errors and unrecoverable infrastructure failures.

3. **All timestamps originate from `IClock`.** No direct calls to `DateTime.Now`, `DateTimeOffset.UtcNow`, or `Environment.TickCount` in domain or application code.

4. **All dimensional values use `Length` (or other geometry value objects).** No `double`, `float`, or bare `decimal` for dimensions in domain or application code. Primitives exist only at the DTO boundary and in persistence models.

5. **Events carry DTOs, not domain objects.** No mutable domain state crosses the event bus.

6. **Settings always resolve to a value.** Built-in defaults guarantee that `ISettingsProvider.Get<T>()` never returns null or throws for a known key.

7. **Logs never affect control flow.** Disabling logging does not change application behavior.

8. **Caches are never authoritative.** A cache miss triggers recomputation from the source of truth. Correctness does not depend on cache presence.

9. **The pipeline is synchronous and single-threaded.** No concurrent command execution, no parallel stage execution, no async stages.

10. **No remote network access is required for core functionality.** The application operates fully offline.

---

## 14. Risks and Edge Cases

| Risk | Mitigation |
|---|---|
| **Synchronous pipeline on UI thread blocks interaction during complex commits** | Performance budgets (§7.1) are set conservatively. If a commit exceeds 200 ms, the specific stage must be profiled and optimized. Moving the pipeline to a background thread is a last resort — it introduces concurrency complexity that violates §9.3 |
| **Log files grow large in long design sessions** | Rolling file strategy with 30-day retention. Individual log files capped at 10 MB. Oldest files pruned on startup |
| **Settings conflict between shop standards and project defaults** | The hierarchy (§5.1) defines precedence. The `ISettingsProvider.GetSource()` method lets the UI show "this value came from shop standards" for transparency |
| **Determinism violations slip through without property-based tests** | Enforce determinism test coverage in CI. Any pipeline stage or domain operation without a determinism test is flagged in code review |
| **Event handler throws, breaking the notification chain** | Event bus catches and logs handler exceptions (§6.2). Remaining handlers still execute. A broken subscriber degrades gracefully |
| **Cache invalidation missed for a specific entity** | `DesignChangedEvent.AffectedEntityIds` is the invalidation signal. If an entity ID is missing from the event, the cache serves stale data. Mitigated by testing that every command produces correct `AffectedEntityIds` |
| **Diagnostic bundle accidentally includes sensitive design content** | The bundle includes command types and timestamps only — no command parameters, no entity state, no geometry. Design content stays in the `.cabinet` file |
| **`async void` in ViewModel command handlers loses exceptions** | ViewModels wrap `async void` handlers in a try/catch that logs and displays errors. Never silently swallowed |
| **SQLite WAL file grows during long sessions without checkpoint** | Periodic WAL checkpoint every 30 seconds (configurable). See persistence_strategy.md §10.4 |
| **Test clock / test ID generator not used in a test, producing non-deterministic results** | Convention: all pipeline and integration tests must inject `TestClock` and `TestIdGenerator`. CI linting rule flags `DateTimeOffset.UtcNow` and `Guid.NewGuid()` in test code |
