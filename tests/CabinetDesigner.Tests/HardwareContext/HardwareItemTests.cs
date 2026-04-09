using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.HardwareContext;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.HardwareContext;

public sealed class HardwareItemTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var pattern = new BoringPattern(Length.FromInches(1.5m), Length.FromInches(0.875m), 2);
        var item = new HardwareItem(
            HardwareItemId.New(),
            "Blum Hinge",
            "71B3550",
            HardwareCategory.Hinge,
            Length.FromInches(12m),
            Length.FromInches(24m),
            pattern,
            Length.FromInches(0.125m));

        Assert.Equal("Blum Hinge", item.Name);
        Assert.Equal(pattern, item.BoringPattern);
        Assert.Equal(Length.FromInches(12m), item.MinOpeningWidth);
    }

    [Fact]
    public void OptionalFields_DefaultToNull()
    {
        var item = new HardwareItem(HardwareItemId.New(), "Knob", null, HardwareCategory.Knob);

        Assert.Null(item.ManufacturerSku);
        Assert.Null(item.MinOpeningWidth);
        Assert.Null(item.MaxOpeningWidth);
        Assert.Null(item.BoringPattern);
        Assert.Null(item.RequiredClearance);
    }

    [Fact]
    public void Constructor_MinGreaterThanMaxThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new HardwareItem(
                HardwareItemId.New(),
                "Slide",
                null,
                HardwareCategory.DrawerSlide,
                Length.FromInches(24m),
                Length.FromInches(18m)));
    }
}
