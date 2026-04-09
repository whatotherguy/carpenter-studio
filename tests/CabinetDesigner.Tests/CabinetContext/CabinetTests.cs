using System;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.CabinetContext;

public sealed class CabinetTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var cabinet = CreateCabinet();

        Assert.Equal("base-36", cabinet.CabinetTypeId);
        Assert.Equal(Length.FromInches(36m), cabinet.NominalWidth);
        Assert.Empty(cabinet.Openings);
        Assert.Empty(cabinet.Overrides);
    }

    [Fact]
    public void Resize_UpdatesWidth()
    {
        var cabinet = CreateCabinet();

        cabinet.Resize(Length.FromInches(42m));

        Assert.Equal(Length.FromInches(42m), cabinet.NominalWidth);
    }

    [Fact]
    public void Resize_NonPositiveWidthThrows()
    {
        var cabinet = CreateCabinet();

        Assert.Throws<InvalidOperationException>(() => cabinet.Resize(Length.Zero));
    }

    [Fact]
    public void AddOpening_AssignsSequentialIndex()
    {
        var cabinet = CreateCabinet();
        var first = cabinet.AddOpening(Length.FromInches(18m), Length.FromInches(30m), OpeningType.SingleDoor);
        var second = cabinet.AddOpening(Length.FromInches(18m), Length.FromInches(30m), OpeningType.Drawer);

        Assert.Equal(0, first.Index);
        Assert.Equal(1, second.Index);
    }

    [Fact]
    public void SetOverride_AddsAndUpdatesOverride()
    {
        var cabinet = CreateCabinet();
        cabinet.SetOverride("toeKickHeight", new OverrideValue.OfLength(Length.FromInches(4m)));
        cabinet.SetOverride("toeKickHeight", new OverrideValue.OfLength(Length.FromInches(5m)));

        var value = Assert.IsType<OverrideValue.OfLength>(cabinet.Overrides["toeKickHeight"]);
        Assert.Equal(Length.FromInches(5m), value.Value);
    }

    [Fact]
    public void RemoveOverride_RemovesValue()
    {
        var cabinet = CreateCabinet();
        cabinet.SetOverride("toeKickHeight", new OverrideValue.OfLength(Length.FromInches(4m)));

        cabinet.RemoveOverride("toeKickHeight");

        Assert.Empty(cabinet.Overrides);
    }

    private static Cabinet CreateCabinet()
        => new(
            CabinetId.New(),
            RevisionId.New(),
            "base-36",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            Length.FromInches(36m),
            Length.FromInches(24m),
            Length.FromInches(34.5m));
}
