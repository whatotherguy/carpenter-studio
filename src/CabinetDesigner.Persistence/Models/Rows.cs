namespace CabinetDesigner.Persistence.Models;

internal sealed class ProjectRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string CurrentState { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}

internal sealed class RevisionRow
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public string State { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? Label { get; set; }
    public string? ApprovalNotes { get; set; }
}

internal sealed class RoomRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string ShapeJson { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class WallRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string StartPoint { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public string Thickness { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class RunRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string WallId { get; set; } = string.Empty;
    public int RunIndex { get; set; }
    public string StartOffset { get; set; } = string.Empty;
    public string EndOffset { get; set; } = string.Empty;
    public string? EndConditionStart { get; set; }
    public string? EndConditionEnd { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class CabinetRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public int SlotIndex { get; set; }
    public string CabinetTypeId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ConstructionMethod { get; set; } = string.Empty;
    public string NominalWidth { get; set; } = string.Empty;
    public string NominalHeight { get; set; } = string.Empty;
    public string NominalDepth { get; set; } = string.Empty;
    public string? OverridesJson { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class PartRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string CabinetId { get; set; } = string.Empty;
    public string PartType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string MaterialId { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public string Width { get; set; } = string.Empty;
    public string Thickness { get; set; } = string.Empty;
    public string? GrainDirection { get; set; }
    public string? EdgeTreatmentJson { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class CommandJournalRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string IntentDescription { get; set; } = string.Empty;
    public string AffectedEntityIds { get; set; } = string.Empty;
    public string? ParentCommandId { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string CommandJson { get; set; } = string.Empty;
    public string DeltasJson { get; set; } = string.Empty;
    public int Succeeded { get; set; }
}

internal sealed class ApprovedSnapshotRow
{
    public string RevisionId { get; set; } = string.Empty;
    public int SnapshotSchemaVer { get; set; }
    public string ApprovedAt { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string DesignBlob { get; set; } = string.Empty;
    public string PartsBlob { get; set; } = string.Empty;
    public string ManufacturingBlob { get; set; } = string.Empty;
    public string InstallBlob { get; set; } = string.Empty;
    public string EstimateBlob { get; set; } = string.Empty;
    public string ValidationBlob { get; set; } = string.Empty;
    public string ExplanationBlob { get; set; } = string.Empty;
}

internal sealed class ExplanationNodeRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string? CommandId { get; set; }
    public int? StageNumber { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public string DecisionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AffectedEntityIds { get; set; } = string.Empty;
    public string? ParentNodeId { get; set; }
    public string? EdgeType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

internal sealed class ValidationIssueRow
{
    public string Id { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string RunAt { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AffectedEntityIds { get; set; } = string.Empty;
    public string? SuggestedFixJson { get; set; }
}

internal sealed class AutosaveCheckpointRow
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string SavedAt { get; set; } = string.Empty;
    public string? LastCommandId { get; set; }
    public int IsClean { get; set; }
}
