using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain;

public abstract record OverrideValue
{
    public sealed record OfLength(Length Value) : OverrideValue;
    public sealed record OfThickness(Thickness Value) : OverrideValue;
    public sealed record OfAngle(Angle Value) : OverrideValue;
    public sealed record OfString(string Value) : OverrideValue;
    public sealed record OfBool(bool Value) : OverrideValue;
    public sealed record OfInt(int Value) : OverrideValue;
    public sealed record OfDecimal(decimal Value) : OverrideValue;
    public sealed record OfMaterialId(MaterialId Value) : OverrideValue;
    public sealed record OfHardwareItemId(HardwareItemId Value) : OverrideValue;
}
