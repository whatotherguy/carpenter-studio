using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Domain;

public sealed class OverrideValueTests
{
    [Fact]
    public void Variants_ConstructCorrectly()
    {
        OverrideValue[] values =
        [
            new OverrideValue.OfLength(Length.FromInches(1m)),
            new OverrideValue.OfThickness(Thickness.Exact(Length.FromInches(0.75m))),
            new OverrideValue.OfAngle(Angle.Right),
            new OverrideValue.OfString("frameless"),
            new OverrideValue.OfBool(true),
            new OverrideValue.OfInt(3),
            new OverrideValue.OfDecimal(2.5m),
            new OverrideValue.OfMaterialId(MaterialId.New()),
            new OverrideValue.OfHardwareItemId(HardwareItemId.New())
        ];

        Assert.Equal(9, values.Length);
    }

    [Fact]
    public void PatternMatching_WorksAcrossVariants()
    {
        OverrideValue value = new OverrideValue.OfLength(Length.FromInches(2m));

        var formatted = value switch
        {
            OverrideValue.OfLength length => length.Value.ToString(),
            _ => "unexpected"
        };

        Assert.Equal("2in", formatted);
    }

    [Fact]
    public void RecordEquality_WorksWithinVariant()
    {
        var first = new OverrideValue.OfString("maple");
        var second = new OverrideValue.OfString("maple");

        Assert.Equal(first, second);
    }
}
