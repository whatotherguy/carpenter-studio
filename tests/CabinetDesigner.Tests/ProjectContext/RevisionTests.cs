using System;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.ProjectContext;

public sealed class RevisionTests
{
    [Fact]
    public void CreateDraft_SetsDraftState()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);

        Assert.Equal(ApprovalState.Draft, revision.State);
        Assert.True(revision.IsEditable);
    }

    [Fact]
    public void IsEditable_IsTrueOnlyForDraft()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);
        revision.TransitionTo(ApprovalState.UnderReview, DateTimeOffset.UnixEpoch.AddMinutes(1));

        Assert.False(revision.IsEditable);
    }

    [Fact]
    public void TransitionTo_ValidTransitionsSucceed()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);

        revision.TransitionTo(ApprovalState.UnderReview, DateTimeOffset.UnixEpoch.AddMinutes(1), "review");
        revision.TransitionTo(ApprovalState.Approved, DateTimeOffset.UnixEpoch.AddMinutes(2), "approved");

        Assert.Equal(ApprovalState.Approved, revision.State);
        Assert.Equal("approved", revision.ApprovalNotes);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(2), revision.ApprovedAt);
    }

    [Fact]
    public void TransitionTo_InvalidTransitionThrows()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            revision.TransitionTo(ApprovalState.Approved, DateTimeOffset.UnixEpoch));

        Assert.Contains("Cannot transition", exception.Message);
    }

    [Fact]
    public void TransitionTo_ApprovedAtSetOnlyWhenApproved()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);
        revision.TransitionTo(ApprovalState.UnderReview, DateTimeOffset.UnixEpoch.AddMinutes(1));

        Assert.Null(revision.ApprovedAt);
    }

    [Fact]
    public void TransitionTo_SupersededIsTerminal()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);
        revision.TransitionTo(ApprovalState.UnderReview, DateTimeOffset.UnixEpoch.AddMinutes(1));
        revision.TransitionTo(ApprovalState.Approved, DateTimeOffset.UnixEpoch.AddMinutes(2));
        revision.TransitionTo(ApprovalState.Superseded, DateTimeOffset.UnixEpoch.AddMinutes(3));

        Assert.Throws<InvalidOperationException>(() =>
            revision.TransitionTo(ApprovalState.Draft, DateTimeOffset.UnixEpoch.AddMinutes(4)));
    }
}
