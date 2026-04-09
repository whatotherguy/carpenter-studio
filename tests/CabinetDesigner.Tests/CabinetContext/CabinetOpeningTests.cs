using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.CabinetContext;

public sealed class CabinetOpeningTests
{
    [Fact]
    public void ChangeType_UpdatesType()
    {
        var opening = CreateOpening();

        opening.ChangeType(OpeningType.Drawer);

        Assert.Equal(OpeningType.Drawer, opening.Type);
    }

    [Fact]
    public void AssignFrontMaterial_SetsMaterialId()
    {
        var opening = CreateOpening();
        var materialId = MaterialId.New();

        opening.AssignFrontMaterial(materialId);

        Assert.Equal(materialId, opening.FrontMaterialId);
    }

    [Fact]
    public void AssignHardware_DoesNotDuplicateHardware()
    {
        var opening = CreateOpening();
        var hardwareId = HardwareItemId.New();

        opening.AssignHardware(hardwareId);
        opening.AssignHardware(hardwareId);

        Assert.Single(opening.HardwareIds);
    }

    private static CabinetOpening CreateOpening()
        => new(
            OpeningId.New(),
            CabinetId.New(),
            Length.FromInches(18m),
            Length.FromInches(30m),
            OpeningType.SingleDoor,
            0);
}
