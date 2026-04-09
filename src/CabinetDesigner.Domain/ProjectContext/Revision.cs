using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.ProjectContext;

public sealed class Revision
{
    private static readonly IReadOnlyDictionary<ApprovalState, ApprovalState[]> AllowedTransitions =
        new Dictionary<ApprovalState, ApprovalState[]>
        {
            [ApprovalState.Draft] = [ApprovalState.UnderReview],
            [ApprovalState.UnderReview] = [ApprovalState.Draft, ApprovalState.Approved],
            [ApprovalState.Approved] = [ApprovalState.LockedForManufacture, ApprovalState.Superseded],
            [ApprovalState.LockedForManufacture] = [ApprovalState.ReleasedToShop, ApprovalState.Superseded],
            [ApprovalState.ReleasedToShop] = [ApprovalState.ReadyForInstall],
            [ApprovalState.ReadyForInstall] = [ApprovalState.Installed],
            [ApprovalState.Installed] = [ApprovalState.Superseded],
            [ApprovalState.Superseded] = []
        };

    public RevisionId Id { get; }
    public ProjectId ProjectId { get; }
    public int VersionNumber { get; }
    public ApprovalState State { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovalNotes { get; private set; }
    public bool IsEditable => State == ApprovalState.Draft;

    private Revision(RevisionId id, ProjectId projectId, int versionNumber, DateTimeOffset createdAt)
    {
        if (projectId == default)
            throw new InvalidOperationException("Revision must belong to a project.");
        if (versionNumber <= 0)
            throw new InvalidOperationException("Revision version number must be positive.");

        Id = id;
        ProjectId = projectId;
        VersionNumber = versionNumber;
        State = ApprovalState.Draft;
        CreatedAt = createdAt;
    }

    public static Revision CreateDraft(ProjectId projectId, int versionNumber, DateTimeOffset createdAt)
        => new(RevisionId.New(), projectId, versionNumber, createdAt);

    public void TransitionTo(ApprovalState newState, DateTimeOffset transitionedAt, string? notes = null)
    {
        if (!AllowedTransitions[State].Contains(newState))
            throw new InvalidOperationException($"Cannot transition from {State} to {newState}.");

        State = newState;
        ApprovalNotes = notes;

        if (newState == ApprovalState.Approved)
            ApprovedAt = transitionedAt;
    }
}
