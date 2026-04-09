# P6 — Application Layer Design

Source: `cabinet_ai_prompt_pack_v4_1_full.md` (Phase 6)
Context: `architecture_summary.md`, `commands.md`, `domain_model.md`, `orchestrator.md`, `why_engine.md`

---

## 1. Goals

- Define the application layer as the **orchestration bridge** between the UI/Editor and the domain
- House all use-case logic — what the system does, not how the domain works
- Stamp command metadata (clock, origin, intent) before submission to the orchestrator
- Expose coarse-grained services to ViewModels — thin, intent-named interfaces
- Own the preview vs. commit distinction (fast path / deep path routing)
- Publish domain events that ViewModels observe — no VM polling internal state
- Never contain domain logic, persistence, or UI concerns

---

## 2. Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│  Presentation Layer (ViewModels)                                  │
│  drag, click, menu, keyboard, numeric entry                       │
└────────────────────┬─────────────────────────────────────────────┘
                     │ calls
                     ▼
┌──────────────────────────────────────────────────────────────────┐
│  Application Layer  [CabinetDesigner.Application]                 │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  Services                                                │    │
│  │  IProjectService, IRunService, IUndoRedoService, ...     │    │
│  └─────────────────────┬────────────────────────────────────┘    │
│                         │ creates commands, calls handlers         │
│  ┌──────────────────────▼─────────────────────────────────────┐  │
│  │  Handlers                                                  │  │
│  │  IDesignCommandHandler   ← stamps metadata, deep path      │  │
│  │  IPreviewCommandHandler  ← fast path only (stages 1–3)     │  │
│  │  IEditorCommandHandler   ← interaction state only          │  │
│  └─────────────────────┬──────────────────────────────────────┘  │
│                         │ IDesignCommand                           │
│  ┌──────────────────────▼─────────────────────────────────────┐  │
│  │  ResolutionOrchestrator                                    │  │
│  │  (coordinates pipeline stages; domain does the work)       │  │
│  └─────────────────────┬──────────────────────────────────────┘  │
│                         │                                          │
│  ┌──────────────────────▼─────────────────────────────────────┐  │
│  │  Domain Events (IApplicationEventBus)                      │  │
│  │  Published after successful command execution               │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
                     │
                     ▼
       Domain / Persistence / Infrastructure
```

**Dependency rule:** Application depends on Domain. Nothing in Application depends on Presentation, WPF, or persistence implementations (only interfaces).

---

## 3. Services

Services are the entry points for ViewModels. They are coarse-grained, intent-named, and delegate all design changes through handlers.

### 3.1 IProjectService

Manages project lifecycle. Never touches domain logic directly — delegates to persistence and orchestrator.

```csharp
namespace CabinetDesigner.Application.Services;

public interface IProjectService
{
    /// <summary>Open an existing project. Returns a summary DTO for binding.</summary>
    Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default);

    /// <summary>Create a new empty project with default shop standards applied.</summary>
    Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default);

    /// <summary>Save the current working state.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Save a named snapshot at the current revision (does not approve).</summary>
    Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default);

    /// <summary>Close the project. Prompts for unsaved-change handling via event.</summary>
    Task CloseAsync();

    ProjectSummaryDto? CurrentProject { get; }
}
```

### 3.2 IRunService

Surfaces run-level intent to ViewModels. Creates the appropriate `IDesignCommand` and routes through the handler.

```csharp
namespace CabinetDesigner.Application.Services;

public interface IRunService
{
    /// <summary>Create a new cabinet run against a wall segment.</summary>
    Task<CommandResultDto> CreateRunAsync(CreateRunRequestDto request);

    /// <summary>Delete a run and optionally its contents.</summary>
    Task<CommandResultDto> DeleteRunAsync(RunId runId);

    /// <summary>Add a cabinet to the end (or specified position) of a run.</summary>
    Task<CommandResultDto> AddCabinetAsync(AddCabinetRequestDto request);

    /// <summary>Insert a cabinet at a specific index within a run.</summary>
    Task<CommandResultDto> InsertCabinetAsync(InsertCabinetRequestDto request);

    /// <summary>Move a cabinet within or between runs.</summary>
    Task<CommandResultDto> MoveCabinetAsync(MoveCabinetRequestDto request);

    /// <summary>Resize a cabinet by nominal width.</summary>
    Task<CommandResultDto> ResizeCabinetAsync(ResizeCabinetRequestDto request);

    /// <summary>Apply a parameter override to a specific cabinet.</summary>
    Task<CommandResultDto> SetCabinetOverrideAsync(SetCabinetOverrideRequestDto request);

    /// <summary>Get a lightweight summary of a run for the panel.</summary>
    RunSummaryDto GetRunSummary(RunId runId);
}
```

### 3.3 IUndoRedoService

Exposes undo/redo to the ViewModel without leaking stack internals.

```csharp
namespace CabinetDesigner.Application.Services;

public interface IUndoRedoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    /// <summary>Undo the most recent design command. Returns the reversal result.</summary>
    CommandResultDto Undo();

    /// <summary>Redo the most recently undone command.</summary>
    CommandResultDto Redo();

    /// <summary>Clear all history. Called on project close.</summary>
    void Clear();
}
```

### 3.4 IValidationSummaryService

Aggregates the current validation state for the status bar and issue panel.

```csharp
namespace CabinetDesigner.Application.Services;

public interface IValidationSummaryService
{
    /// <summary>All current issues across the project, ordered by severity descending.</summary>
    IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues();

    /// <summary>Issues scoped to a specific entity (cabinet, run, etc.).</summary>
    IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId);

    /// <summary>True if any ManufactureBlocker issues exist.</summary>
    bool HasManufactureBlockers { get; }
}
```

### 3.5 ISnapshotService

Creates and loads approved revision snapshots.

```csharp
namespace CabinetDesigner.Application.Services;

public interface ISnapshotService
{
    /// <summary>Approve and freeze the current revision. Returns the immutable snapshot.</summary>
    Task<RevisionDto> ApproveRevisionAsync(string label, CancellationToken ct = default);

    /// <summary>Load a previously approved snapshot (read-only view).</summary>
    Task<RevisionDto> LoadSnapshotAsync(Guid revisionId, CancellationToken ct = default);

    IReadOnlyList<RevisionDto> GetRevisionHistory();
}
```

---

## 4. Handlers

Handlers translate service-level requests into typed commands, stamp required metadata, and route to the orchestrator or editor state manager.

### 4.1 IDesignCommandHandler

The deep-path handler. Used for all committed design changes.

```csharp
namespace CabinetDesigner.Application.Handlers;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Stamps command metadata (timestamp from IClock, origin, intent description),
/// runs structural pre-validation, then submits to ResolutionOrchestrator for full pipeline execution.
/// Publishes domain events after successful execution.
/// </summary>
public interface IDesignCommandHandler
{
    /// <summary>
    /// Execute a design command through the full 11-stage pipeline.
    /// Called on commit (drop, confirm, numeric entry).
    /// </summary>
    CommandResultDto Execute(IDesignCommand command);
}
```

**Implementation contract:**

```csharp
// Pseudocode
public CommandResultDto Execute(IDesignCommand command)
{
    // 1. Structural pre-validation (stateless, pure)
    var issues = command.ValidateStructure();
    if (issues.Any(i => i.Severity >= ValidationSeverity.Error))
        return CommandResultDto.Rejected(command.Metadata, command.CommandType, issues);

    // 2. Submit to orchestrator (deep path — all 11 stages)
    var result = _orchestrator.Execute(command);

    // 3. Map to DTO — CommandType comes from command, not CommandMetadata
    var dto = CommandResultDto.From(result, command.CommandType);

    // 4. Publish events if successful
    if (result.Success)
        _eventBus.Publish(new DesignChangedEvent(dto));

    return dto;
}
```

### 4.2 IPreviewCommandHandler

The fast-path handler. Used during drag operations for lightweight preview.

```csharp
namespace CabinetDesigner.Application.Handlers;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Runs stages 1–3 only (input capture, interaction interpretation, spatial resolution).
/// Does NOT commit state. Does NOT push to undo stack. Does NOT record explanation nodes.
/// Returns a lightweight preview result for canvas rendering.
/// </summary>
public interface IPreviewCommandHandler
{
    /// <summary>
    /// Execute the fast path for drag-time preview.
    /// Safe to call continuously during mouse move — no side effects on committed state.
    /// </summary>
    PreviewResultDto Preview(IDesignCommand command);
}
```

### 4.3 IEditorCommandHandler

Handles editor-only interaction commands that never touch design state.

```csharp
namespace CabinetDesigner.Application.Handlers;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Applies editor interaction commands (selection change, zoom, pan, mode switch).
/// Bypasses ResolutionOrchestrator entirely.
/// Updates editor interaction state only.
/// </summary>
public interface IEditorCommandHandler
{
    void Execute(IEditorCommand command);
}
```

---

## 5. DTOs

DTOs cross the application/presentation boundary. They are plain C# records — no domain logic, no geometry value objects exposed to the ViewModel (all rendered as display-safe values).

**Primitive transport rule:** `decimal` dimensions in DTOs (e.g., `NominalWidthInches`) are boundary-only transport values. They exist solely because ViewModels cannot hold geometry value objects. The suffix `Inches` is a deliberate signal — not a unit assumption. The service layer is the only place that converts these primitives into `Length`, `Point2D`, etc. No primitive dimension value crosses into the domain unchecked.

### 5.1 ProjectSummaryDto

```csharp
namespace CabinetDesigner.Application.DTOs;

public sealed record ProjectSummaryDto(
    Guid ProjectId,
    string Name,
    string FilePath,
    DateTimeOffset LastModified,
    string CurrentRevisionLabel,
    bool HasUnsavedChanges);
```

### 5.2 RevisionDto

```csharp
namespace CabinetDesigner.Application.DTOs;

public sealed record RevisionDto(
    Guid RevisionId,
    string Label,
    DateTimeOffset CreatedAt,
    string ApprovalState,   // draft | under_review | approved | locked_for_manufacture | ...
    bool IsApproved,
    bool IsLocked);
```

### 5.3 CommandResultDto

Serializable result returned to ViewModels after every command execution. No domain types leak out.

```csharp
namespace CabinetDesigner.Application.DTOs;

public sealed record CommandResultDto(
    Guid CommandId,
    string CommandType,      // e.g. "layout.add_cabinet_to_run" — from IDesignCommand.CommandType
    bool Success,
    IReadOnlyList<ValidationIssueSummaryDto> Issues,
    IReadOnlyList<string> AffectedEntityIds,
    IReadOnlyList<string> ExplanationNodeIds)
{
    /// <summary>
    /// commandType comes from IDesignCommand.CommandType, not CommandMetadata.
    /// The handler passes it in because CommandResult only carries CommandMetadata.
    /// </summary>
    public static CommandResultDto From(CommandResult result, string commandType) => new(
        result.CommandMetadata.CommandId.Value,
        commandType,
        result.Success,
        result.Issues.Select(ValidationIssueSummaryDto.From).ToList(),
        result.CommandMetadata.AffectedEntityIds,
        result.ExplanationNodeIds.Select(n => n.Value.ToString()).ToList());

    public static CommandResultDto Rejected(
        CommandMetadata metadata,
        string commandType,
        IReadOnlyList<ValidationIssue> issues) => new(
        metadata.CommandId.Value,
        commandType,
        false,
        issues.Select(ValidationIssueSummaryDto.From).ToList(),
        metadata.AffectedEntityIds,
        []);
}
```

### 5.4 ValidationIssueSummaryDto

```csharp
namespace CabinetDesigner.Application.DTOs;

public sealed record ValidationIssueSummaryDto(
    string Severity,         // Info | Warning | Error | ManufactureBlocker
    string Code,
    string Message,
    IReadOnlyList<string>? AffectedEntityIds)
{
    public static ValidationIssueSummaryDto From(ValidationIssue issue) => new(
        issue.Severity.ToString(),
        issue.Code,
        issue.Message,
        issue.AffectedEntityIds);
}
```

### 5.5 PreviewResultDto

Returned by `IPreviewCommandHandler.Preview()` — fast-path only.

```csharp
namespace CabinetDesigner.Application.DTOs;

public sealed record PreviewResultDto(
    bool IsValid,
    string? RejectionReason,
    IReadOnlyList<PlacementCandidateDto> Candidates,    // ranked snap candidates
    IReadOnlyList<ValidationIssueSummaryDto> Warnings); // non-blocking issues shown during drag
```

### 5.6 RunSummaryDto

```csharp
namespace CabinetDesigner.Application.DTOs;

public sealed record RunSummaryDto(
    Guid RunId,
    string WallId,
    decimal TotalNominalWidthInches,
    int CabinetCount,
    bool HasFillers,
    bool HasValidationErrors,
    IReadOnlyList<RunSlotSummaryDto> Slots);

public sealed record RunSlotSummaryDto(
    Guid CabinetId,
    string CabinetTypeId,
    decimal NominalWidthInches,
    int Index);
```

### 5.7 Request DTOs

Service methods receive request DTOs — typed intent carriers from the ViewModel.

```csharp
namespace CabinetDesigner.Application.DTOs;

public sealed record CreateRunRequestDto(
    string WallId,
    decimal StartXInches,
    decimal StartYInches,
    decimal EndXInches,
    decimal EndYInches);

public sealed record AddCabinetRequestDto(
    Guid RunId,
    string CabinetTypeId,
    decimal NominalWidthInches,
    string Placement);           // StartOfRun | EndOfRun | AtIndex

public sealed record InsertCabinetRequestDto(
    Guid RunId,
    string CabinetTypeId,
    decimal NominalWidthInches,
    int InsertAtIndex,
    Guid LeftNeighborId,
    Guid RightNeighborId);

public sealed record MoveCabinetRequestDto(
    Guid CabinetId,
    Guid SourceRunId,
    Guid TargetRunId,
    string TargetPlacement,
    int? TargetIndex);

public sealed record ResizeCabinetRequestDto(
    Guid CabinetId,
    decimal CurrentNominalWidthInches,
    decimal NewNominalWidthInches);

/// <summary>
/// Discriminated union for override values at the DTO boundary.
/// Mirrors domain OverrideValue but uses primitives only — no geometry types cross the boundary.
/// The service converts to domain OverrideValue before constructing the command.
/// </summary>
public abstract record OverrideValueDto
{
    public sealed record OfDecimalInches(decimal Inches) : OverrideValueDto;
    public sealed record OfString(string Value) : OverrideValueDto;
    public sealed record OfBool(bool Value) : OverrideValueDto;
    public sealed record OfInt(int Value) : OverrideValueDto;
    public sealed record OfMaterialId(string MaterialId) : OverrideValueDto;
    public sealed record OfHardwareItemId(string HardwareItemId) : OverrideValueDto;
}

public sealed record SetCabinetOverrideRequestDto(
    Guid CabinetId,
    string ParameterKey,
    OverrideValueDto Value);
```

---

## 6. Integration

### 6.1 Event Flow

```
ViewModel
  │
  │ calls service method (e.g., IRunService.AddCabinetAsync)
  ▼
RunService
  │ constructs IDesignCommand with typed parameters
  │ uses IClock for timestamp, CommandOrigin.User, intent string
  ▼
IDesignCommandHandler.Execute(command)
  │ ValidateStructure() → reject early if structural errors
  ▼
IResolutionOrchestrator.Execute(command)
  │ runs full 11-stage pipeline
  │ captures deltas, explanation nodes, pushes undo entry
  ▼
CommandResult
  │ mapped to CommandResultDto
  ▼
IApplicationEventBus.Publish(DesignChangedEvent)
  │
  ▼
ViewModel (subscribed to event)
  │ updates observable properties
  ▼
WPF bindings refresh canvas, panels, status bar
```

### 6.2 Preview vs Commit

| Aspect | Preview (fast path) | Commit (deep path) |
|---|---|---|
| **Trigger** | Mouse move during drag | Drop, Enter, menu confirm |
| **Handler** | `IPreviewCommandHandler.Preview()` | `IDesignCommandHandler.Execute()` |
| **Stages run** | 1–3 (spatial only) | All 11 |
| **State mutated** | No — preview is non-destructive | Yes — working state updated |
| **Undo entry** | Not pushed | Pushed |
| **Explanation nodes** | Not recorded | Recorded |
| **Events published** | No | Yes — `DesignChangedEvent` |
| **Return type** | `PreviewResultDto` | `CommandResultDto` |
| **When aborted** | Drag cancelled → no cleanup needed | Not applicable |

### 6.3 Command Construction Ownership

The application layer owns the moment of command construction — this is where:
- `IClock.Now` is read (never in the domain)
- `CommandOrigin` is set based on context (User, Editor, System, Template)
- `IntentDescription` is generated from service context
- Request DTOs are converted to typed geometry value objects (`Length`, `Point2D`, etc.)

```csharp
// Example: RunService.AddCabinetAsync constructs the command
var command = new AddCabinetToRunCommand(
    runId: new RunId(request.RunId),
    cabinetTypeId: request.CabinetTypeId,
    nominalWidth: Length.FromInches(request.NominalWidthInches),
    placement: Enum.Parse<RunPlacement>(request.Placement),
    origin: CommandOrigin.User,
    intentDescription: $"Add {request.NominalWidthInches}\" {request.CabinetTypeId} to run",
    timestamp: _clock.Now);
```

### 6.4 IApplicationEventBus

Published after successful deep-path execution. ViewModels subscribe — they do not poll.

**Delivery semantics:**
- **Synchronous, in-order** — `Publish` calls all handlers before returning. Events are dispatched in subscription order. No background threads.
- **Caller thread** — events are raised on whatever thread called `Publish`. For WPF, this is always the UI thread (all commands enter via ViewModel → service). Handlers must not block.
- **No throw propagation** — if a handler throws, the exception is caught, logged, and delivery continues to remaining handlers. A broken subscriber must not abort the event chain.
- **No queuing, no replay** — events are fire-and-forget at the point of publication. A ViewModel that subscribes after an event fires will not receive it retroactively; it reads current state from services instead.

```csharp
namespace CabinetDesigner.Application.Events;

public interface IApplicationEventBus
{
    void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent;
}

public interface IApplicationEvent { }

public sealed record DesignChangedEvent(CommandResultDto Result) : IApplicationEvent;
public sealed record ProjectOpenedEvent(ProjectSummaryDto Project) : IApplicationEvent;
public sealed record ProjectClosedEvent(Guid ProjectId) : IApplicationEvent;
public sealed record RevisionApprovedEvent(RevisionDto Revision) : IApplicationEvent;
public sealed record UndoAppliedEvent(CommandResultDto Result) : IApplicationEvent;
public sealed record RedoAppliedEvent(CommandResultDto Result) : IApplicationEvent;
```

### 6.5 Dependency Injection Registration (Application Module)

```csharp
// CabinetDesigner.App — DI wiring
services.AddSingleton<IResolutionOrchestrator, ResolutionOrchestrator>();
services.AddSingleton<IWhyEngine, WhyEngine>();
services.AddSingleton<IUndoStack, UndoStack>();
services.AddSingleton<IApplicationEventBus, ApplicationEventBus>();

services.AddScoped<IDesignCommandHandler, DesignCommandHandler>();
services.AddScoped<IPreviewCommandHandler, PreviewCommandHandler>();
services.AddScoped<IEditorCommandHandler, EditorCommandHandler>();

services.AddScoped<IProjectService, ProjectService>();
services.AddScoped<IRunService, RunService>();
services.AddScoped<IUndoRedoService, UndoRedoService>();
services.AddScoped<IValidationSummaryService, ValidationSummaryService>();
services.AddScoped<ISnapshotService, SnapshotService>();
```

---

## 7. Non-Responsibilities (Explicit Exclusions)

| Excluded Concern | Owned By |
|---|---|
| Domain invariants, entity logic, filler math | `CabinetDesigner.Domain` |
| Pipeline stage implementations | `CabinetDesigner.Application.Pipeline` (inside `ResolutionOrchestrator`) |
| WPF bindings, ViewModel state, canvas rendering | `CabinetDesigner.Presentation` / `CabinetDesigner.Rendering` |
| SQLite queries, repository implementations | `CabinetDesigner.Persistence` |
| File system, logging, settings | `CabinetDesigner.Infrastructure` |
| Drag/drop mechanics, snap evaluation | `CabinetDesigner.Editor` |
| Cut list export, shop drawings | `CabinetDesigner.Exports` |
