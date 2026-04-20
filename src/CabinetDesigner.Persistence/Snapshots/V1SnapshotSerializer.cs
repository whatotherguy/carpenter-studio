namespace CabinetDesigner.Persistence.Snapshots;

public sealed class V1SnapshotSerializer : ISnapshotSerializer
{
    public ApprovedSnapshot Serialize(
        PersistedProjectState state,
        IReadOnlyList<ExplanationNodeRecord> explanationNodes,
        IReadOnlyList<ValidationIssueRecord> validationIssues)
    {
        var approvedAt = DateTimeOffset.UtcNow;
        var label = state.Revision.Label ?? $"Rev {state.Revision.RevisionNumber}";

        return new ApprovedSnapshot(
            state.Revision.Id,
            state.Project.Id,
            state.Revision.RevisionNumber,
            approvedAt,
            state.Revision.ApprovedBy,
            label,
            string.Empty,
            JsonSerializer.Serialize(new OrderedBlob<WorkingRevision>(1, state.Revision.Id.Value, approvedAt, state.WorkingRevision), SqliteJson.Options),
            JsonSerializer.Serialize(new OrderedBlob<IReadOnlyList<CabinetDesigner.Application.Pipeline.StageResults.GeneratedPart>>(1, state.Revision.Id.Value, approvedAt, state.WorkingRevision.Parts), SqliteJson.Options),
            JsonSerializer.Serialize(new OrderedBlob<object>(1, state.Revision.Id.Value, approvedAt, new { state.WorkingRevision.Parts.Count }), SqliteJson.Options),
            JsonSerializer.Serialize(new OrderedBlob<object>(1, state.Revision.Id.Value, approvedAt, new { state.WorkingRevision.Cabinets.Count }), SqliteJson.Options),
            JsonSerializer.Serialize(new OrderedBlob<object>(1, state.Revision.Id.Value, approvedAt, new { state.Project.Name }), SqliteJson.Options),
            JsonSerializer.Serialize(new OrderedBlob<IReadOnlyList<ValidationIssueRecord>>(1, state.Revision.Id.Value, approvedAt, validationIssues), SqliteJson.Options),
            JsonSerializer.Serialize(new OrderedBlob<IReadOnlyList<ExplanationNodeRecord>>(1, state.Revision.Id.Value, approvedAt, explanationNodes), SqliteJson.Options));
    }

    private sealed record OrderedBlob<T>(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("revision_id")] Guid RevisionId,
        [property: JsonPropertyName("approved_at")] DateTimeOffset ApprovedAt,
        [property: JsonPropertyName("payload")] T Payload);
}
