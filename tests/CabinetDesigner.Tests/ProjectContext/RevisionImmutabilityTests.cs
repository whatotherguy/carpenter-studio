using System;
using System.Linq;
using CabinetDesigner.Application;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.ProjectContext;
using Xunit;

namespace CabinetDesigner.Tests.ProjectContext;

public sealed class RevisionImmutabilityTests
{
    [Fact]
    public void NonDraftRevision_IsReportedAsNonEditable()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);

        revision.TransitionTo(ApprovalState.UnderReview, DateTimeOffset.UnixEpoch.AddMinutes(1));

        Assert.False(revision.IsEditable);
    }

    [Fact]
    public void DesignCommands_DoNotCarryRevisionState_ForContextualValidation()
    {
        var constructorParameters = typeof(ResizeCabinetCommand)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(RevisionId), constructorParameters);
        Assert.DoesNotContain(typeof(ApprovalState), constructorParameters);
        Assert.NotNull(typeof(IResolutionOrchestrator));
    }

    [Fact]
    public void CabinetOpening_MutationsRemainAvailableWithoutRevisionContext()
    {
        var revision = Revision.CreateDraft(ProjectId.New(), 1, DateTimeOffset.UnixEpoch);
        revision.TransitionTo(ApprovalState.UnderReview, DateTimeOffset.UnixEpoch.AddMinutes(1));

        var opening = new CabinetOpening(
            OpeningId.New(),
            CabinetId.New(),
            Length.FromInches(18m),
            Length.FromInches(30m),
            OpeningType.SingleDoor,
            0);

        opening.ChangeType(OpeningType.Drawer);

        Assert.False(revision.IsEditable);
        Assert.Equal(OpeningType.Drawer, opening.Type);
    }
}
