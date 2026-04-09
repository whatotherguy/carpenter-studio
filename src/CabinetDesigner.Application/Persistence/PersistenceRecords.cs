using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Domain.Validation;

namespace CabinetDesigner.Application.Persistence;

public sealed record ProjectRecord(
    ProjectId Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ApprovalState CurrentState,
    string? FilePath = null);

public sealed record RevisionRecord(
    RevisionId Id,
    ProjectId ProjectId,
    int RevisionNumber,
    ApprovalState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    string? Label,
    string? ApprovalNotes = null);

public sealed record WorkingRevision(
    RevisionRecord Revision,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Wall> Walls,
    IReadOnlyList<CabinetRun> Runs,
    IReadOnlyList<Cabinet> Cabinets,
    IReadOnlyList<GeneratedPart> Parts);

public sealed record CommandJournalEntry(
    CommandId Id,
    RevisionId RevisionId,
    int SequenceNumber,
    string CommandType,
    CommandOrigin Origin,
    string IntentDescription,
    IReadOnlyList<string> AffectedEntityIds,
    CommandId? ParentCommandId,
    DateTimeOffset Timestamp,
    string CommandJson,
    IReadOnlyList<StateDelta> Deltas,
    bool Succeeded);

public sealed record ApprovedSnapshot(
    RevisionId RevisionId,
    ProjectId ProjectId,
    int RevisionNumber,
    DateTimeOffset ApprovedAt,
    string? ApprovedBy,
    string Label,
    string DesignBlob,
    string PartsBlob,
    string ManufacturingBlob,
    string InstallBlob,
    string EstimateBlob,
    string ValidationBlob,
    string ExplanationBlob);

public sealed record SnapshotSummary(
    RevisionId RevisionId,
    ProjectId ProjectId,
    int RevisionNumber,
    string Label,
    DateTimeOffset ApprovedAt,
    string? ApprovedBy);

public sealed record ExplanationNodeRecord(
    ExplanationNodeId Id,
    RevisionId RevisionId,
    CommandId? CommandId,
    int? StageNumber,
    ExplanationNodeType NodeType,
    string DecisionType,
    string Description,
    IReadOnlyList<string> AffectedEntityIds,
    ExplanationNodeId? ParentNodeId,
    string? EdgeType,
    ExplanationNodeStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ValidationIssueRecord(
    ValidationIssueId Id,
    RevisionId RevisionId,
    DateTimeOffset RunAt,
    ValidationSeverity Severity,
    string RuleCode,
    string Message,
    IReadOnlyList<string> AffectedEntityIds,
    string? SuggestedFixJson);

public sealed record AutosaveCheckpoint(
    string Id,
    ProjectId ProjectId,
    RevisionId RevisionId,
    DateTimeOffset SavedAt,
    CommandId? LastCommandId,
    bool IsClean);

public sealed record PersistedProjectState(
    ProjectRecord Project,
    RevisionRecord Revision,
    WorkingRevision WorkingRevision,
    AutosaveCheckpoint? Checkpoint);
