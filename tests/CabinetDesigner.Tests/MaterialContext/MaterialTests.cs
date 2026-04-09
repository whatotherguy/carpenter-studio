using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;
using Xunit;

namespace CabinetDesigner.Tests.MaterialContext;

public sealed class MaterialTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var thickness = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        var material = new Material(
            MaterialId.New(),
            "Maple Ply",
            "PLY-MAPLE-3/4",
            MaterialCategory.Plywood,
            thickness,
            GrainDirection.LengthWise,
            Length.FromInches(48m),
            Length.FromInches(96m));

        Assert.Equal("Maple Ply", material.Name);
        Assert.Equal(thickness, material.SheetThickness);
        Assert.Equal(Length.FromInches(48m), material.SheetWidth);
    }

    [Fact]
    public void Constructor_UsesThicknessValueObject()
    {
        var thickness = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        var material = new Material(
            MaterialId.New(),
            "Birch Ply",
            null,
            MaterialCategory.Plywood,
            thickness,
            GrainDirection.LengthWise,
            Length.FromInches(48m),
            Length.FromInches(96m));

        Assert.Equal(Length.FromInches(0.75m), material.SheetThickness.Nominal);
        Assert.Equal(Length.FromInches(0.72m), material.SheetThickness.Actual);
    }

    [Fact]
    public void Constructor_InvalidGrainForMdfThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new Material(
                MaterialId.New(),
                "MDF",
                null,
                MaterialCategory.MDF,
                Thickness.Exact(Length.FromInches(0.75m)),
                GrainDirection.LengthWise,
                Length.FromInches(49m),
                Length.FromInches(97m)));
    }
}
