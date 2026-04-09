using System;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Identifiers;

public sealed class AdditionalIdentifierTests
{
    [Fact]
    public void NewIdentifiers_New_ReturnUniqueValues()
    {
        AssertUnique(ProjectId.New(), ProjectId.New());
        AssertUnique(RevisionId.New(), RevisionId.New());
        AssertUnique(RoomId.New(), RoomId.New());
        AssertUnique(WallId.New(), WallId.New());
        AssertUnique(WallOpeningId.New(), WallOpeningId.New());
        AssertUnique(ObstacleId.New(), ObstacleId.New());
        AssertUnique(RunSlotId.New(), RunSlotId.New());
        AssertUnique(FillerId.New(), FillerId.New());
        AssertUnique(OpeningId.New(), OpeningId.New());
        AssertUnique(MaterialId.New(), MaterialId.New());
        AssertUnique(HardwareItemId.New(), HardwareItemId.New());
        AssertUnique(TemplateId.New(), TemplateId.New());
    }

    [Fact]
    public void NewIdentifiers_ToString_ReturnsGuidString()
    {
        var projectGuid = Guid.NewGuid();
        var revisionGuid = Guid.NewGuid();

        Assert.Equal(projectGuid.ToString(), new ProjectId(projectGuid).ToString());
        Assert.Equal(revisionGuid.ToString(), new RevisionId(revisionGuid).ToString());
    }

    private static void AssertUnique<T>(T first, T second) where T : struct
    {
        Assert.NotEqual(first, second);
    }
}
