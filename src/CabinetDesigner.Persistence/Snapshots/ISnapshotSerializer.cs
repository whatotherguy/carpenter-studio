namespace CabinetDesigner.Persistence.Snapshots;

public interface ISnapshotSerializer
{
    ApprovedSnapshot Serialize(
        PersistedProjectState state,
        IReadOnlyList<ExplanationNodeRecord> explanationNodes,
        IReadOnlyList<ValidationIssueRecord> validationIssues);
}
