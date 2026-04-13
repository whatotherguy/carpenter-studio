using System;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using Xunit;

namespace CabinetDesigner.Tests.ProjectContext;

public sealed class ProjectTests
{
    [Fact]
    public void Constructor_CreatesInitialDraftRevision()
    {
        var createdAt = new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.Zero);

        var project = new Project(ProjectId.New(), "Kitchen Remodel", createdAt);

        Assert.Single(project.Revisions);
        Assert.NotNull(project.CurrentRevision);
        Assert.Equal(ApprovalState.Draft, project.CurrentRevision.State);
        Assert.Equal(1, project.CurrentRevision.VersionNumber);
        Assert.Equal(createdAt, project.LastModifiedAt);
    }

    [Fact]
    public void CurrentRevision_ReturnsMostRecentRevision()
    {
        var createdAt = DateTimeOffset.UnixEpoch;
        var project = new Project(ProjectId.New(), "Kitchen Remodel", createdAt);
        var latest = project.CreateNewRevision(createdAt.AddHours(2));

        Assert.NotNull(project.CurrentRevision);
        Assert.Equal(latest.Id, project.CurrentRevision.Id);
    }

    [Fact]
    public void CreateNewRevision_AddsRevisionAndTouchesTimestamp()
    {
        var createdAt = DateTimeOffset.UnixEpoch;
        var project = new Project(ProjectId.New(), "Kitchen Remodel", createdAt);
        var revisionTime = createdAt.AddDays(1);

        var revision = project.CreateNewRevision(revisionTime);

        Assert.Equal(2, project.Revisions.Count);
        Assert.Equal(2, revision.VersionNumber);
        Assert.Equal(revisionTime, project.LastModifiedAt);
    }

    [Fact]
    public void UpdateName_ChangesNameAndTouchesTimestamp()
    {
        var project = new Project(ProjectId.New(), "Original", DateTimeOffset.UnixEpoch);
        var modifiedAt = DateTimeOffset.UnixEpoch.AddMinutes(30);

        project.UpdateName("Updated", modifiedAt);

        Assert.Equal("Updated", project.Name);
        Assert.Equal(modifiedAt, project.LastModifiedAt);
    }

    [Fact]
    public void Revisions_IsAlwaysNonEmpty()
    {
        var project = new Project(ProjectId.New(), "Kitchen Remodel", DateTimeOffset.UnixEpoch);

        Assert.NotEmpty(project.Revisions);
    }

    [Fact]
    public void CurrentRevision_WithMultipleRevisions_ReturnsHighestVersionNumber()
    {
        var createdAt = DateTimeOffset.UnixEpoch;
        var project = new Project(ProjectId.New(), "Kitchen Remodel", createdAt);

        var revision2 = project.CreateNewRevision(createdAt.AddHours(1));
        var revision3 = project.CreateNewRevision(createdAt.AddHours(2));

        Assert.Equal(3, project.Revisions.Count);
        Assert.Equal(revision3.Id, project.CurrentRevision?.Id);
        Assert.Equal(3, project.CurrentRevision?.VersionNumber);
    }
}
