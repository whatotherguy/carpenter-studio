using CabinetDesigner.Persistence.Mapping;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Mapping;

public sealed class LengthAndConverterTests
{
    [Theory]
    [InlineData(0.125)]
    [InlineData(12.0)]
    [InlineData(36.375)]
    [InlineData(36.3754)]
    public void LengthText_RoundTripsDeterministically(double inches)
    {
        var value = Length.FromInches((decimal)inches);
        var serialized = LengthText.FormatLength(value);

        Assert.EndsWith("in", serialized, StringComparison.Ordinal);
        Assert.Equal(value, LengthText.ParseLength(serialized));
    }

    [Fact]
    public void PointText_RoundTripsDeterministically()
    {
        var point = new Point2D(12.345m, 67.890m);
        var serialized = LengthText.FormatPoint(point);

        Assert.Equal(point, LengthText.ParsePoint(serialized));
    }

    [Fact]
    public void ThicknessText_RoundTripsDeterministically()
    {
        var thickness = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        var serialized = LengthText.FormatThickness(thickness);

        Assert.Equal(thickness, LengthText.ParseThickness(serialized));
    }

    [Fact]
    public void OverrideValueJsonConverter_RoundTripsAllSupportedCases()
    {
        var values = new OverrideValue[]
        {
            new OverrideValue.OfLength(Length.FromInches(36)),
            new OverrideValue.OfThickness(Thickness.Exact(Length.FromInches(0.75m))),
            new OverrideValue.OfAngle(Angle.FromDegrees(45)),
            new OverrideValue.OfString("oak"),
            new OverrideValue.OfBool(true),
            new OverrideValue.OfInt(5),
            new OverrideValue.OfDecimal(12.5m),
            new OverrideValue.OfMaterialId(MaterialId.New()),
            new OverrideValue.OfHardwareItemId(HardwareItemId.New())
        };

        foreach (var value in values)
        {
            var json = JsonSerializer.Serialize(value, SqliteJson.Options);
            var roundTrip = JsonSerializer.Deserialize<OverrideValue>(json, SqliteJson.Options);
            Assert.Equal(value, roundTrip);
        }
    }

    [Fact]
    public void DeltaValueJsonConverter_RoundTripsAllSupportedCases()
    {
        var values = new DeltaValue[]
        {
            new DeltaValue.OfLength(Length.FromInches(30)),
            new DeltaValue.OfThickness(Thickness.Exact(Length.FromInches(0.75m))),
            new DeltaValue.OfString("value"),
            new DeltaValue.OfBool(false),
            new DeltaValue.OfInt(42),
            new DeltaValue.OfDecimal(1.25m),
            new DeltaValue.Null()
        };

        foreach (var value in values)
        {
            var json = JsonSerializer.Serialize(value, SqliteJson.Options);
            var roundTrip = JsonSerializer.Deserialize<DeltaValue>(json, SqliteJson.Options);
            Assert.Equal(value, roundTrip);
        }
    }
}
