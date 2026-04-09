using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Commands;

public sealed class DeltaValueTests
{
    [Fact]
    public void OfLength_StoresValue()
    {
        var value = Length.FromInches(30m);
        var deltaValue = new DeltaValue.OfLength(value);

        Assert.Equal(value, deltaValue.Value);
    }

    [Fact]
    public void OfThickness_StoresValue()
    {
        var value = Thickness.Exact(Length.FromInches(0.75m));
        var deltaValue = new DeltaValue.OfThickness(value);

        Assert.Equal(value, deltaValue.Value);
    }

    [Fact]
    public void OfString_StoresValue()
    {
        var deltaValue = new DeltaValue.OfString("base-36");

        Assert.Equal("base-36", deltaValue.Value);
    }

    [Fact]
    public void OfBool_TrueAndFalse_StoreCorrectly()
    {
        Assert.True(new DeltaValue.OfBool(true).Value);
        Assert.False(new DeltaValue.OfBool(false).Value);
    }

    [Fact]
    public void OfInt_StoresValue()
    {
        var deltaValue = new DeltaValue.OfInt(4);

        Assert.Equal(4, deltaValue.Value);
    }

    [Fact]
    public void OfDecimal_StoresValue()
    {
        var deltaValue = new DeltaValue.OfDecimal(1.25m);

        Assert.Equal(1.25m, deltaValue.Value);
    }

    [Fact]
    public void Null_IsDistinctVariant()
    {
        DeltaValue deltaValue = new DeltaValue.Null();

        Assert.IsType<DeltaValue.Null>(deltaValue);
    }

    [Fact]
    public void PatternMatch_ReachesCorrectBranch()
    {
        DeltaValue deltaValue = new DeltaValue.OfLength(Length.FromInches(42m));

        var branch = deltaValue switch
        {
            DeltaValue.OfLength ofLength when ofLength.Value == Length.FromInches(42m) => "length",
            DeltaValue.OfString => "string",
            DeltaValue.OfBool => "bool",
            DeltaValue.OfInt => "int",
            DeltaValue.OfDecimal => "decimal",
            DeltaValue.OfThickness => "thickness",
            DeltaValue.Null => "null",
            _ => "unknown"
        };

        Assert.Equal("length", branch);
    }
}
